export interface SourceExcerpt {
  pageNumber: number
  excerpt: string
}

export interface ChatResponse {
  documentId: string
  question: string
  answer: string
  hasEvidence: boolean
  sources: SourceExcerpt[]
}

export type ChatMessage =
  | { id: string; role: 'user'; content: string }
  | {
      id: string
      role: 'assistant'
      content: string
      hasEvidence: boolean
      sources: SourceExcerpt[]
    }
  | { id: string; role: 'error'; content: string }
