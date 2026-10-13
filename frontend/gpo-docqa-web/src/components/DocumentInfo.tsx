import type { DocumentResponse } from '../types/document'

interface DocumentInfoProps {
  document: DocumentResponse | null
  error: string | null
}

export function DocumentInfo({ document, error }: DocumentInfoProps) {
  if (error) return <p className="error-message">{error}</p>
  if (!document) return <p className="placeholder">Loading document…</p>

  const isProcessing = document.status === 'Pending' || document.status === 'Processing'

  return (
    <div className="document-info">
      <div className="status-line">
        <strong>{document.fileName}</strong>
        <span className={`badge badge-${document.status.toLowerCase()}`}>{document.status}</span>
        {document.pageCount !== null && (
          <span className="page-count">
            {document.pageCount} pages
            {document.chunkCount !== null && ` · ${document.chunkCount} chunks`}
          </span>
        )}
      </div>
      {isProcessing && <p className="status-line">Extracting text and generating embeddings…</p>}
      {document.status === 'Failed' && document.errorMessage && (
        <p className="error-message">{document.errorMessage}</p>
      )}
    </div>
  )
}
