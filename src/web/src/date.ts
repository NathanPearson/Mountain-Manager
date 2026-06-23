import { DateTime } from 'luxon'

export function formatDueDate(value: string) {
  return DateTime.fromISO(value, { zone: 'utc' }).toFormat('LLL d, yyyy')
}

export function todayIsoDate() {
  return DateTime.local().toISODate()
}
