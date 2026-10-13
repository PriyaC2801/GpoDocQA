const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5169'

export class ApiError extends Error {
  status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
}

async function parseErrorMessage(response: Response): Promise<string> {
  try {
    const problem: ProblemDetails = await response.json()
    return problem.detail ?? problem.title ?? response.statusText
  } catch {
    return response.statusText || `Request failed with status ${response.status}`
  }
}

export async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, init)

  if (!response.ok) {
    throw new ApiError(await parseErrorMessage(response), response.status)
  }

  return (await response.json()) as T
}
