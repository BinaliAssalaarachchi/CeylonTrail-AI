import apiClient from './client'

export async function getReportOverview() {
  const response = await apiClient.get('/api/reports/overview')
  return response.data
}
