import apiClient from './client'

export async function getTravelAlerts(params) {
  const queryParams = Object.fromEntries(
    Object.entries(params).filter(([, value]) => value !== '' && value !== null && value !== undefined),
  )
  const response = await apiClient.get('/api/travel-alerts', { params: queryParams })
  return response.data
}
