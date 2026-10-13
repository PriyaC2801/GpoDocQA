import { useEffect, useState } from 'react'
import './App.css'
import { getCurrentDocument } from './api/documentsApi'
import { ChatPanel } from './components/ChatPanel'
import type { DocumentResponse } from './types/document'

const POLL_INTERVAL_MS = 2000

function App() {
  const [document, setDocument] = useState<DocumentResponse | null>(null)

  // The backend indexes the bundled document at startup, so poll until it's done.
  useEffect(() => {
    let cancelled = false
    let timer: ReturnType<typeof setTimeout> | undefined

    async function load() {
      try {
        const current = await getCurrentDocument()
        if (cancelled) return
        setDocument(current)
        if (current.status === 'Pending' || current.status === 'Processing') {
          timer = setTimeout(load, POLL_INTERVAL_MS)
        }
      } catch (err) {
        if (cancelled) return
        // Nothing renders this any more, so log it - otherwise an unreachable API is
        // indistinguishable from a slow one, with only the disabled input as a hint.
        console.error('Could not load the document, retrying…', err)
        timer = setTimeout(load, POLL_INTERVAL_MS)
      }
    }

    load()
    return () => {
      cancelled = true
      clearTimeout(timer)
    }
  }, [])

  return (
    <div className="app">
      <header className="app-header">
        <h1>GPO Document Q&amp;A</h1>
        <p className="subtitle">Ask questions answered only from the GPO Annual Report.</p>
      </header>

      <main className="app-main">
        <section className="panel chat-panel" aria-label="Chat">
          <h2>Chat</h2>
          <ChatPanel document={document} />
        </section>
      </main>
    </div>
  )
}

export default App
