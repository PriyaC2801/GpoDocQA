export type DocumentStatus = 'Pending' | 'Processing' | 'Ready' | 'Failed'

export interface DocumentResponse {
  documentId: string
  fileName: string
  status: DocumentStatus
  pageCount: number | null
  chunkCount: number | null
  errorMessage: string | null
}
