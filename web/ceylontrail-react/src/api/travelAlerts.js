import apiClient from './client'

export async function getTravelAlerts(params) {
  const queryParams = Object.fromEntries(
    Object.entries(params).filter(([, value]) => value !== '' && value !== null && value !== undefined),
  )
  const response = await apiClient.get('/api/travel-alerts', { params: queryParams })
  return response.data
}

export async function getTravelAlert(id) {
  const response = await apiClient.get(`/api/travel-alerts/${id}`)
  return response.data
}

export async function createTravelAlert(payload) {
  const response = await apiClient.post('/api/travel-alerts', payload)
  return response.data
}

export async function updateTravelAlert(id, payload) {
  const response = await apiClient.put(`/api/travel-alerts/${id}`, payload)
  return response.data
}

export async function deleteTravelAlert(id) {
  await apiClient.delete(`/api/travel-alerts/${id}`)
}
