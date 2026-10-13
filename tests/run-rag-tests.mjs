#!/usr/bin/env node
// Scores the RAG pipeline against questions with known answers, so prompt, chunking
// or model changes can be re-checked against the same bar.
//
//   node --use-system-ca tests/run-rag-tests.mjs
//   node --use-system-ca tests/run-rag-tests.mjs --only procurement-total
//   node --use-system-ca tests/run-rag-tests.mjs --verbose
//
// Exit codes: 0 all passed | 1 a case failed | 2 setup problem | 3 Gemini quota exhausted

import { readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

// The API serves HTTPS with the ASP.NET dev certificate. Node ignores the Windows
// certificate store unless started with --use-system-ca (Node 23+), so without that
// flag fall back to skipping verification - tolerable only because this talks to
// localhost, and it makes the script still run on older Node.
if (!process.execArgv.includes('--use-system-ca')) {
  process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'
}

const here = dirname(fileURLToPath(import.meta.url))
const argv = process.argv.slice(2)
const flag = (name) => {
  const i = argv.indexOf(name)
  return i >= 0 ? argv[i + 1] : undefined
}

const apiBase = flag('--api') ?? 'https://localhost:7003'
const only = flag('--only')
const verbose = argv.includes('--verbose')

const normalize = (text) => text.toLowerCase().replace(/\s+/g, ' ')

async function loadDocument() {
  let response
  try {
    response = await fetch(`${apiBase}/api/documents/current`)
  } catch (cause) {
    throw new Error(
      `Cannot reach the API at ${apiBase}. Start it with ` +
        `\`dotnet run --launch-profile https\` in backend/GpoDocQA.Api. (${cause.message})`,
    )
  }

  if (!response.ok) {
    throw new Error(`GET /api/documents/current returned ${response.status}`)
  }

  const document = await response.json()
  if (document.status !== 'Ready') {
    throw new Error(
      `Document status is "${document.status}", expected "Ready".` +
        (document.errorMessage ? ` API says: ${document.errorMessage}` : '') +
        (document.status === 'Failed'
          ? '\nIf indexing failed, check that the pgvector container is up: docker compose up -d'
          : '\nIndexing may still be running - try again in a moment.'),
    )
  }

  return document
}

async function ask(documentId, question) {
  const response = await fetch(`${apiBase}/api/chat/ask`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ documentId, question }),
  })

  const body = await response.text()

  if (!response.ok) {
    // A 429 from Gemini surfaces as a 500 problem-details response here; either way it
    // is a quota problem, not a regression, so it must not be reported as a failure.
    if (/quota|RESOURCE_EXHAUSTED|429/i.test(body)) {
      const quotaError = new Error('Gemini quota exhausted')
      quotaError.isQuota = true
      throw quotaError
    }
    throw new Error(`POST /api/chat/ask returned ${response.status}: ${body.slice(0, 300)}`)
  }

  return JSON.parse(body)
}

function score(testCase, result) {
  const failures = []
  const answer = normalize(result.answer ?? '')

  if (typeof testCase.expectEvidence === 'boolean' && result.hasEvidence !== testCase.expectEvidence) {
    failures.push(`hasEvidence was ${result.hasEvidence}, expected ${testCase.expectEvidence}`)
  }

  for (const needle of testCase.mustInclude ?? []) {
    if (!answer.includes(normalize(needle))) {
      failures.push(`answer is missing "${needle}"`)
    }
  }

  for (const group of testCase.mustIncludeAny ?? []) {
    if (!group.some((needle) => answer.includes(normalize(needle)))) {
      failures.push(`answer has none of: ${group.map((n) => `"${n}"`).join(', ')}`)
    }
  }

  for (const needle of testCase.mustNotInclude ?? []) {
    if (answer.includes(normalize(needle))) {
      failures.push(`answer wrongly contains "${needle}"`)
    }
  }

  // Retrieval is checked separately from generation: a correct answer built on the wrong
  // excerpts still signals a retrieval problem worth knowing about.
  if (testCase.expectSourcePages) {
    const pages = (result.sources ?? []).map((source) => source.pageNumber)
    if (!pages.some((page) => testCase.expectSourcePages.includes(page))) {
      failures.push(
        `no source page from [${testCase.expectSourcePages.join(', ')}]; got [${pages.join(', ') || 'none'}]`,
      )
    }
  }

  return failures
}

const { cases } = JSON.parse(await readFile(join(here, 'rag-cases.json'), 'utf8'))
const selected = only ? cases.filter((c) => c.id === only) : cases

if (selected.length === 0) {
  console.error(`No case matches --only ${only}. Available: ${cases.map((c) => c.id).join(', ')}`)
  process.exit(2)
}

let document
try {
  document = await loadDocument()
} catch (error) {
  console.error(`\nSetup failed: ${error.message}\n`)
  process.exit(2)
}

console.log(`\nAPI      ${apiBase}`)
console.log(`Document ${document.fileName} - ${document.pageCount} pages, ${document.chunkCount} chunks`)
console.log(`Cases    ${selected.length}\n`)

let passed = 0
const failed = []

for (const testCase of selected) {
  process.stdout.write(`  ${testCase.id} ... `)

  let result
  const startedAt = Date.now()
  try {
    result = await ask(document.documentId, testCase.question)
  } catch (error) {
    if (error.isQuota) {
      console.log('SKIPPED (quota)')
      console.error(
        '\nGemini chat quota is exhausted (free tier is ~20 requests/day per model).' +
          '\nWait for the reset, or switch Gemini:ChatModel in appsettings.json.\n',
      )
      process.exit(3)
    }
    console.log('ERROR')
    failed.push({ id: testCase.id, reasons: [error.message] })
    continue
  }

  const elapsed = ((Date.now() - startedAt) / 1000).toFixed(1)
  const reasons = score(testCase, result)

  if (reasons.length === 0) {
    passed += 1
    console.log(`PASS (${elapsed}s)`)
  } else {
    console.log(`FAIL (${elapsed}s)`)
    failed.push({ id: testCase.id, reasons, answer: result.answer })
  }

  if (verbose) {
    console.log(`      Q: ${testCase.question}`)
    console.log(`      A: ${result.answer}`)
    console.log(`      evidence=${result.hasEvidence} pages=[${(result.sources ?? []).map((s) => s.pageNumber).join(', ')}]`)
  }
}

if (failed.length > 0) {
  console.log('\nFailures:')
  for (const failure of failed) {
    console.log(`\n  ${failure.id}`)
    for (const reason of failure.reasons) {
      console.log(`    - ${reason}`)
    }
    if (failure.answer) {
      console.log(`    answer: ${failure.answer}`)
    }
  }
}

console.log(`\n${passed}/${selected.length} passed\n`)
process.exit(failed.length > 0 ? 1 : 0)
