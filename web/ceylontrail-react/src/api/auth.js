import apiClient from './client'

export async function login(credentials) {
  const response = await apiClient.post('/api/auth/login', credentials)
  return response.data
}
