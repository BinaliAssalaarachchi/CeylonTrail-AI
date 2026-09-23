export function apiErrorMessage(error, fallback = 'The request could not be completed.') {
  if (error.response?.status === 401) return 'Your session has expired. Please sign in again.'
  if (error.response?.status === 403) return 'You do not have permission to manage this attraction.'
  if (error.response?.status === 404) return 'The attraction or resource could not be found.'
  return error.response?.data?.message || fallback
}

export function timeValue(value) { return value ? value.slice(0, 5) : '' }
