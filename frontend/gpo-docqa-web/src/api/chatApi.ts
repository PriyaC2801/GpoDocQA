import { apiRequest } from './client'
import type { ChatResponse } from '../types/chat'

export function askQuestion(documentId: string, question: string): Promise<ChatResponse> {
  return apiRequest<ChatResponse>('/api/chat/ask', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ documentId, question }),
  })
}
