import type {
  ApiError,
  ApiResponse,
  DueBucketFilter,
  AuthResponse,
  PriorityFilter,
  TaskFormValues,
  TaskItem,
  TaskStatusFilter,
} from './types'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5033'
const USER_TIME_ZONE = Intl.DateTimeFormat().resolvedOptions().timeZone

export class ApiClientError extends Error {
  public readonly apiError: ApiError
  public readonly traceId: string

  constructor(apiError: ApiError, traceId: string) {
    super(apiError.message)
    this.apiError = apiError
    this.traceId = traceId
  }
}

async function request<T>(path: string, options: RequestInit = {}, token?: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(USER_TIME_ZONE ? { 'X-Time-Zone': USER_TIME_ZONE } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
  })

  const envelope = (await response.json()) as ApiResponse<T>

  if (!response.ok || !envelope.success) {
    throw new ApiClientError(
      envelope.error ?? { code: 'REQUEST_FAILED', message: 'The request failed.' },
      envelope.traceId,
    )
  }

  return envelope.data as T
}

export function register(email: string, password: string) {
  return request<AuthResponse>('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
}

export function login(email: string, password: string) {
  return request<AuthResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
}

export function listTasks(token: string, status: TaskStatusFilter, priority: PriorityFilter, dueBucket: DueBucketFilter) {
  const params = new URLSearchParams()
  if (status !== 'all') {
    params.set('status', status)
  }
  if (priority !== 'all') {
    params.set('priority', priority)
  }
  if (dueBucket !== 'all') {
    params.set('dueBucket', dueBucket)
  }

  const query = params.toString()
  return request<TaskItem[]>(`/api/tasks${query ? `?${query}` : ''}`, {}, token)
}

export function createTask(token: string, values: TaskFormValues) {
  return request<TaskItem>(
    '/api/tasks',
    {
      method: 'POST',
      body: JSON.stringify(values),
    },
    token,
  )
}

export function updateTask(token: string, id: string, values: TaskFormValues) {
  return request<TaskItem>(
    `/api/tasks/${id}`,
    {
      method: 'PUT',
      body: JSON.stringify(values),
    },
    token,
  )
}

export function setTaskCompletion(token: string, id: string, isCompleted: boolean) {
  return request<TaskItem>(
    `/api/tasks/${id}/completion`,
    {
      method: 'PATCH',
      body: JSON.stringify({ isCompleted }),
    },
    token,
  )
}

export function deleteTask(token: string, id: string) {
  return request<Record<string, never>>(
    `/api/tasks/${id}`,
    {
      method: 'DELETE',
    },
    token,
  )
}
