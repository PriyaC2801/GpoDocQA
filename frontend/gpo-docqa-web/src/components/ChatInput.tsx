import { useState } from 'react'

interface ChatInputProps {
  disabled: boolean
  placeholder: string
  onSend: (question: string) => void
}

export function ChatInput({ disabled, placeholder, onSend }: ChatInputProps) {
  const [value, setValue] = useState('')

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    const question = value.trim()
    if (!question || disabled) {
      return
    }
    onSend(question)
    setValue('')
  }

  return (
    <form className="chat-input" onSubmit={handleSubmit}>
      <input
        type="text"
        value={value}
        onChange={(event) => setValue(event.target.value)}
        placeholder={placeholder}
        disabled={disabled}
      />
      <button type="submit" disabled={disabled || !value.trim()}>
        Send
      </button>
    </form>
  )
}
