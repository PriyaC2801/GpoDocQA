import { apiRequest } from './client'
import type { DocumentResponse } from '../types/document'

export function getCurrentDocument(): Promise<DocumentResponse> {
  return apiRequest<DocumentResponse>('/api/documents/current')
}
