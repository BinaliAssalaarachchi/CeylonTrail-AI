import axios from 'axios'

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5027',
  headers: { 'Content-Type': 'application/json' },
})

apiClient.interceptors.request.use((config) => {
  const storedAuth = localStorage.getItem('ceylontrail.auth')
  if (storedAuth) {
    try {
      const { token } = JSON.parse(storedAuth)
      if (token) config.headers.Authorization = `Bearer ${token}`
    } catch {
      localStorage.removeItem('ceylontrail.auth')
    }
  }
  return config
})

export default apiClient
