import type { ChatMessage } from '../types/chat'

interface MessageBubbleProps {
  message: ChatMessage
}

export function MessageBubble({ message }: MessageBubbleProps) {
  if (message.role === 'user') {
    return (
      <div className="message message-user">
        <p>{message.content}</p>
      </div>
    )
  }

  if (message.role === 'error') {
    return (
      <div className="message message-error">
        <p>{message.content}</p>
      </div>
    )
  }

  return (
    <div className={`message message-assistant${message.hasEvidence ? '' : ' message-no-evidence'}`}>
      <p>{message.content}</p>
    </div>
  )
}
