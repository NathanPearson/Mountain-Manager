import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  AlertTriangle,
  CalendarClock,
  Check,
  LogOut,
  Pencil,
  Plus,
  RefreshCcw,
  Save,
  Trash2,
  X,
} from 'lucide-react'
import {
  ApiClientError,
  createTask,
  deleteTask,
  listTasks,
  login,
  register,
  setTaskCompletion,
  updateTask,
} from './api'
import { formatDueDate, todayIsoDate } from './date'
import type {
  AuthResponse,
  DueBucket,
  DueBucketFilter,
  Priority,
  PriorityFilter,
  TaskFormValues,
  TaskItem,
  TaskStatusFilter,
} from './types'
import './styles.css'

const priorities: Priority[] = ['Low', 'Medium', 'High', 'Urgent']
const dueBuckets: DueBucket[] = ['Overdue', 'Today', 'Upcoming', 'Completed']
const dueBucketFilters: DueBucketFilter[] = ['all', 'Overdue', 'Today', 'Upcoming', 'Completed']
const emailPattern = /^[^@\s]+@[^@\s]+\.[^@\s]+$/
const isoDatePattern = /^(\d{4})-(\d{2})-(\d{2})$/

const emptyForm: TaskFormValues = {
  title: '',
  description: '',
  priority: 'Medium',
  dueDate: todayIsoDate(),
}

function getStoredSession() {
  const raw = localStorage.getItem('mountain-manager-session')
  return raw ? (JSON.parse(raw) as AuthResponse) : null
}

function getErrorMessage(error: unknown) {
  if (error instanceof ApiClientError) {
    if (error.apiError.code === 'UNAUTHORIZED') {
      return 'Email and password do not match our records.'
    }

    const detailText = error.apiError.details
      ? Object.values(error.apiError.details).flat().join(' ')
      : error.apiError.message

    return detailText
  }

  return 'Something went wrong. Please try again.'
}

function requiredMessage(field: string) {
  return `${field} is required.`
}

function getTaskFormErrors(details: Record<string, string[]>) {
  const errors: Partial<Record<keyof TaskFormValues, string>> = {}

  for (const key of ['title', 'description', 'priority', 'dueDate'] as const) {
    const message = details[key]?.[0]
    if (message) {
      errors[key] = message
    }
  }

  return errors
}

function App() {
  const [session, setSession] = useState<AuthResponse | null>(() => getStoredSession())
  const [tasks, setTasks] = useState<TaskItem[]>([])
  const [statusFilter, setStatusFilter] = useState<TaskStatusFilter>('active')
  const [priorityFilter, setPriorityFilter] = useState<PriorityFilter>('all')
  const [dueBucketFilter, setDueBucketFilter] = useState<DueBucketFilter>('all')
  const [form, setForm] = useState<TaskFormValues>(emptyForm)
  const [formErrors, setFormErrors] = useState<Partial<Record<keyof TaskFormValues, string>>>({})
  const [editingTask, setEditingTask] = useState<TaskItem | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  const token = session?.token

  async function loadTasks() {
    if (!token) {
      return
    }

    setIsLoading(true)
    setMessage(null)

    try {
      setTasks(await listTasks(token, statusFilter, priorityFilter, dueBucketFilter))
    } catch (error) {
      setMessage(getErrorMessage(error))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadTasks()
  }, [token, statusFilter, priorityFilter, dueBucketFilter])

  function saveSession(nextSession: AuthResponse) {
    localStorage.setItem('mountain-manager-session', JSON.stringify(nextSession))
    setSession(nextSession)
  }

  function signOut() {
    localStorage.removeItem('mountain-manager-session')
    setSession(null)
    setTasks([])
  }

  async function handleSubmitTask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!token) {
      return
    }

    setIsLoading(true)
    setMessage(null)

    const dueDateInput = event.currentTarget.elements.namedItem('dueDate') as HTMLInputElement | null
    const nextErrors = validateTaskForm(form, Boolean(dueDateInput?.validity.badInput))
    setFormErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) {
      setIsLoading(false)
      return
    }

    try {
      if (editingTask) {
        await updateTask(token, editingTask.id, form)
      } else {
        await createTask(token, form)
      }

      setForm(emptyForm)
      setFormErrors({})
      setEditingTask(null)
      await loadTasks()
    } catch (error) {
      if (error instanceof ApiClientError && error.apiError.details) {
        setFormErrors((current) => ({ ...current, ...getTaskFormErrors(error.apiError.details ?? {}) }))
      }

      setMessage(getErrorMessage(error))
    } finally {
      setIsLoading(false)
    }
  }

  function startEditing(task: TaskItem) {
    setEditingTask(task)
    setFormErrors({})
    setForm({
      title: task.title,
      description: task.description ?? '',
      priority: task.priority,
      dueDate: task.dueDate,
    })
  }

  async function toggleCompletion(task: TaskItem) {
    if (!token) {
      return
    }

    setMessage(null)
    try {
      await setTaskCompletion(token, task.id, !task.isCompleted)
      await loadTasks()
    } catch (error) {
      setMessage(getErrorMessage(error))
    }
  }

  async function removeTask(task: TaskItem) {
    if (!token) {
      return
    }

    setMessage(null)
    try {
      await deleteTask(token, task.id)
      await loadTasks()
    } catch (error) {
      setMessage(getErrorMessage(error))
    }
  }

  const groupedTasks = useMemo(() => {
    return dueBuckets.map((bucket) => ({
      bucket,
      tasks: tasks.filter((task) => task.dueBucket === bucket),
    }))
  }, [tasks])

  if (!session) {
    return <AuthScreen onAuthenticated={saveSession} />
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">Mountain Manager</p>
          <h1>Mountains</h1>
        </div>
        <div className="user-menu">
          <span className="user-email">{session.user.email}</span>
          <button className="icon-button" type="button" onClick={signOut} aria-label="Sign out" title="Sign out">
            <LogOut size={18} />
          </button>
        </div>
      </header>

      {message ? (
        <div className="alert" role="alert">
          <AlertTriangle size={18} />
          <span>{message}</span>
        </div>
      ) : null}

      <section className="layout-grid">
        <aside className="panel task-form-panel">
          <div className="panel-heading">
            <h2>{editingTask ? 'Edit Task' : 'New Task'}</h2>
            {editingTask ? (
              <button
                className="icon-button"
                type="button"
                onClick={() => {
                  setEditingTask(null)
                  setForm(emptyForm)
                  setFormErrors({})
                }}
                aria-label="Cancel edit"
                title="Cancel edit"
              >
                <X size={18} />
              </button>
            ) : null}
          </div>

          <form className="task-form" onSubmit={handleSubmitTask} noValidate>
            <label className={formErrors.title ? 'field has-error' : 'field'}>
              <span>
                Title <strong aria-hidden="true">*</strong>
              </span>
              <input
                value={form.title}
                aria-invalid={Boolean(formErrors.title)}
                onChange={(event) => {
                  setForm((current) => ({ ...current, title: event.target.value }))
                  setFormErrors((current) => ({ ...current, title: undefined }))
                }}
                placeholder="Schedule follow-up"
                maxLength={120}
              />
              {formErrors.title ? <em>{formErrors.title}</em> : null}
            </label>

            <label className="field">
              <span>Description</span>
              <textarea
                value={form.description}
                onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
                placeholder="Optional context"
                rows={4}
                maxLength={2000}
              />
            </label>

            <div className="form-row">
              <label className="field">
                <span>Priority</span>
                <select
                  value={form.priority}
                  onChange={(event) => setForm((current) => ({ ...current, priority: event.target.value as Priority }))}
                >
                  {priorities.map((priority) => (
                    <option key={priority} value={priority}>
                      {priority}
                    </option>
                  ))}
                </select>
              </label>

              <label className={formErrors.dueDate ? 'field has-error' : 'field'}>
                <span>
                  Due Date <strong aria-hidden="true">*</strong>
                </span>
                <input
                  name="dueDate"
                  type="date"
                  value={form.dueDate}
                  aria-invalid={Boolean(formErrors.dueDate)}
                  onChange={(event) => {
                    setForm((current) => ({ ...current, dueDate: event.target.value }))
                    setFormErrors((current) => ({ ...current, dueDate: undefined }))
                  }}
                  onInput={(event) => {
                    setForm((current) => ({ ...current, dueDate: (event.target as HTMLInputElement).value }))
                    setFormErrors((current) => ({ ...current, dueDate: undefined }))
                  }}
                />
                {formErrors.dueDate ? <em>{formErrors.dueDate}</em> : null}
              </label>
            </div>

            {Object.values(formErrors).some(Boolean) ? (
              <div className="form-summary" role="alert">
                {Object.values(formErrors)
                  .filter(Boolean)
                  .map((error) => (
                    <span key={error}>{error}</span>
                  ))}
              </div>
            ) : null}

            <button className="primary-button" type="submit" disabled={isLoading}>
              {editingTask ? <Save size={18} /> : <Plus size={18} />}
              <span>{editingTask ? 'Save Task' : 'Add Task'}</span>
            </button>
          </form>
        </aside>

        <section className="task-board">
          <div className="toolbar">
            <div className="filters">
              <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as TaskStatusFilter)}>
                <option value="active">Active</option>
                <option value="completed">Completed</option>
                <option value="all">All</option>
              </select>

              <select value={priorityFilter} onChange={(event) => setPriorityFilter(event.target.value as PriorityFilter)}>
                <option value="all">All priorities</option>
                {priorities.map((priority) => (
                  <option key={priority} value={priority}>
                    {priority}
                  </option>
                ))}
              </select>

              <select value={dueBucketFilter} onChange={(event) => setDueBucketFilter(event.target.value as DueBucketFilter)}>
                <option value="all">All due dates</option>
                {dueBucketFilters
                  .filter((bucket) => bucket !== 'all')
                  .map((bucket) => (
                    <option key={bucket} value={bucket}>
                      {bucket}
                    </option>
                  ))}
              </select>
            </div>

            <button className="secondary-button" type="button" onClick={loadTasks} disabled={isLoading}>
              <RefreshCcw size={16} />
              <span>Refresh</span>
            </button>
          </div>

          {isLoading && tasks.length === 0 ? <div className="empty-state">Loading tasks...</div> : null}

          {!isLoading && tasks.length === 0 ? (
            <div className="empty-state">
              <CalendarClock size={28} />
              <span>No tasks match the current filters.</span>
            </div>
          ) : null}

          {groupedTasks.map(({ bucket, tasks: bucketTasks }) =>
            bucketTasks.length > 0 ? (
              <TaskBucket
                key={bucket}
                bucket={bucket}
                tasks={bucketTasks}
                onEdit={startEditing}
                onDelete={removeTask}
                onToggle={toggleCompletion}
              />
            ) : null,
          )}
        </section>
      </section>
    </main>
  )
}

function AuthScreen({ onAuthenticated }: { onAuthenticated: (session: AuthResponse) => void }) {
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<{ email?: string; password?: string }>({})
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSubmitting(true)
    setError(null)

    const nextErrors = validateAuthForm(email, password)
    setFieldErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) {
      setIsSubmitting(false)
      return
    }

    try {
      const response = mode === 'login' ? await login(email, password) : await register(email, password)
      onAuthenticated(response)
    } catch (requestError) {
      setError(getErrorMessage(requestError))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="auth-shell">
      <section className="auth-panel">
        <div>
          <p className="eyebrow">Mountain Manager</p>
          <h1>{mode === 'login' ? 'Sign in' : 'Create account'}</h1>
        </div>

        {error ? (
          <div className="alert" role="alert">
            <AlertTriangle size={18} />
            <span>{error}</span>
          </div>
        ) : null}

        <form className="task-form" onSubmit={handleSubmit} noValidate>
          <label className={fieldErrors.email ? 'field has-error' : 'field'}>
            <span>
              Email <strong aria-hidden="true">*</strong>
            </span>
            <input
              value={email}
              aria-invalid={Boolean(fieldErrors.email)}
              onChange={(event) => {
                setEmail(event.target.value)
                setFieldErrors((current) => ({ ...current, email: undefined }))
              }}
              type="email"
              autoComplete="email"
            />
            {fieldErrors.email ? <em>{fieldErrors.email}</em> : null}
          </label>

          <label className={fieldErrors.password ? 'field has-error' : 'field'}>
            <span>
              Password <strong aria-hidden="true">*</strong>
            </span>
            <input
              value={password}
              aria-invalid={Boolean(fieldErrors.password)}
              onChange={(event) => {
                setPassword(event.target.value)
                setFieldErrors((current) => ({ ...current, password: undefined }))
              }}
              type="password"
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
            />
            {fieldErrors.password ? <em>{fieldErrors.password}</em> : null}
          </label>

          {Object.values(fieldErrors).some(Boolean) ? (
            <div className="form-summary" role="alert">
              {Object.values(fieldErrors)
                .filter(Boolean)
                .map((fieldError) => (
                  <span key={fieldError}>{fieldError}</span>
                ))}
            </div>
          ) : null}

          <button className="primary-button" type="submit" disabled={isSubmitting}>
            <Check size={18} />
            <span>{mode === 'login' ? 'Sign in' : 'Register'}</span>
          </button>
        </form>

        <button
          className="text-button"
          type="button"
          onClick={() => {
            setMode(mode === 'login' ? 'register' : 'login')
            setError(null)
            setFieldErrors({})
          }}
        >
          {mode === 'login' ? 'Need an account? Register' : 'Already have an account? Sign in'}
        </button>
      </section>
    </main>
  )
}

function validateTaskForm(values: TaskFormValues, hasInvalidDueDateInput = false) {
  const errors: Partial<Record<keyof TaskFormValues, string>> = {}

  if (!values.title.trim()) {
    errors.title = requiredMessage('Title')
  }

  if (hasInvalidDueDateInput) {
    errors.dueDate = 'Enter a valid due date.'
  } else if (!values.dueDate) {
    errors.dueDate = requiredMessage('Due date')
  } else if (!isValidIsoDate(values.dueDate)) {
    errors.dueDate = 'Enter a valid due date.'
  }

  return errors
}

function isValidIsoDate(value: string) {
  const match = isoDatePattern.exec(value)
  if (!match) {
    return false
  }

  const year = Number(match[1])
  const month = Number(match[2])
  const day = Number(match[3])
  const parsed = new Date(Date.UTC(year, month - 1, day))

  return parsed.getUTCFullYear() === year && parsed.getUTCMonth() === month - 1 && parsed.getUTCDate() === day
}

function validateAuthForm(email: string, password: string) {
  const errors: { email?: string; password?: string } = {}

  if (!email.trim()) {
    errors.email = requiredMessage('Email')
  } else if (!emailPattern.test(email.trim())) {
    errors.email = 'Enter a valid email address.'
  }

  if (!password) {
    errors.password = requiredMessage('Password')
  }

  return errors
}

function TaskBucket({
  bucket,
  tasks,
  onEdit,
  onDelete,
  onToggle,
}: {
  bucket: DueBucket
  tasks: TaskItem[]
  onEdit: (task: TaskItem) => void
  onDelete: (task: TaskItem) => void
  onToggle: (task: TaskItem) => void
}) {
  return (
    <section className={`bucket bucket-${bucket.toLowerCase()}`}>
      <div className="bucket-heading">
        <h2>{bucket}</h2>
        <span>{tasks.length}</span>
      </div>

      <div className="task-list">
        {tasks.map((task) => (
          <article className={task.isCompleted ? 'task-row completed' : 'task-row'} key={task.id}>
            <button
              className="complete-button"
              type="button"
              onClick={() => onToggle(task)}
              aria-label={task.isCompleted ? 'Mark task incomplete' : 'Mark task complete'}
              title={task.isCompleted ? 'Mark incomplete' : 'Mark complete'}
            >
              <Check size={18} />
            </button>

            <div className="task-content">
              <div className="task-title-line">
                <h3>{task.title}</h3>
                <span className={`priority priority-${task.priority.toLowerCase()}`}>{task.priority}</span>
              </div>
              {task.description ? <p>{task.description}</p> : null}
              <span className="due-date">Due {formatDueDate(task.dueDate)}</span>
            </div>

            <div className="task-actions">
              <button className="icon-button" type="button" onClick={() => onEdit(task)} aria-label="Edit task" title="Edit">
                <Pencil size={17} />
              </button>
              <button
                className="icon-button danger"
                type="button"
                onClick={() => onDelete(task)}
                aria-label="Delete task"
                title="Delete"
              >
                <Trash2 size={17} />
              </button>
            </div>
          </article>
        ))}
      </div>
    </section>
  )
}

export default App
