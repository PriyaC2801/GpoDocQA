import type { SourceExcerpt } from '../types/chat'

interface SourceListProps {
  sources: SourceExcerpt[]
}

export function SourceList({ sources }: SourceListProps) {
  if (sources.length === 0) {
    return null
  }

  return (
    <div className="source-list">
      <p className="source-list-label">Sources</p>
      {sources.map((source, index) => (
        <details key={`${source.pageNumber}-${index}`} className="source-item">
          <summary>Page {source.pageNumber}</summary>
          <p className="source-excerpt">{source.excerpt}</p>
        </details>
      ))}
    </div>
  )
}
