import { useState } from 'react'
import { askQuestion } from '../api/chatApi'
import { ApiError } from '../api/client'
import type { ChatMessage } from '../types/chat'
import type { DocumentResponse } from '../types/document'
import { ChatInput } from './ChatInput'
import { MessageBubble } from './MessageBubble'

interface ChatPanelProps {
  document: DocumentResponse | null
}

function placeholderFor(document: DocumentResponse | null): string {
  if (!document) return ''
  if (document.status === 'Ready') return 'Ask a question about the document…'
  if (document.status === 'Failed') return 'This document failed to process'
  return 'Document is still processing…'
}

export function ChatPanel({ document }: ChatPanelProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [isAsking, setIsAsking] = useState(false)

  const canAsk = document?.status === 'Ready' && !isAsking

  async function handleSend(question: string) {
    const activeDocument = document
    if (!activeDocument) return

    setMessages((prev) => [...prev, { id: crypto.randomUUID(), role: 'user', content: question }])
    setIsAsking(true)

    try {
      const response = await askQuestion(activeDocument.documentId, question)
      setMessages((prev) => [
        ...prev,
        {
          id: crypto.randomUUID(),
          role: 'assistant',
          content: response.answer,
          hasEvidence: response.hasEvidence,
          sources: response.sources,
        },
      ])
    } catch (err) {
      setMessages((prev) => [
        ...prev,
        {
          id: crypto.randomUUID(),
          role: 'error',
          content: err instanceof ApiError ? err.message : 'Something went wrong. Please try again.',
        },
      ])
    } finally {
      setIsAsking(false)
    }
  }

  return (
    <div className="chat-panel-content">
      <div className="chat-history">
        {messages.length === 0 && document && (
          <p className="placeholder">Ask a question about the document.</p>
        )}
        {messages.map((message) => (
          <MessageBubble key={message.id} message={message} />
        ))}
        {isAsking && <div className="message message-assistant message-loading">Thinking…</div>}
      </div>
      <ChatInput disabled={!canAsk} placeholder={placeholderFor(document)} onSend={handleSend} />
    </div>
  )
}
