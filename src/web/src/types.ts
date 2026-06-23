export type Priority = 'Low' | 'Medium' | 'High' | 'Urgent'
export type DueBucket = 'Overdue' | 'Today' | 'Upcoming' | 'Completed'
export type TaskStatusFilter = 'all' | 'active' | 'completed'
export type PriorityFilter = 'all' | Priority
export type DueBucketFilter = 'all' | DueBucket

export type ApiError = {
  code: string
  message: string
  details?: Record<string, string[]>
}

export type ApiResponse<T> = {
  success: boolean
  data: T | null
  error: ApiError | null
  traceId: string
}

export type User = {
  id: string
  email: string
}

export type AuthResponse = {
  token: string
  user: User
  expiresAt: string
}

export type TaskItem = {
  id: string
  title: string
  description: string | null
  priority: Priority
  dueDate: string
  dueBucket: DueBucket
  isCompleted: boolean
  createdAt: string
  updatedAt: string
  completedAt: string | null
}

export type TaskFormValues = {
  title: string
  description: string
  priority: Priority
  dueDate: string
}
