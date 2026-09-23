import apiClient from './client'

export async function getApprovalRequests(status = '') {
  const response = await apiClient.get('/api/approval-requests', {
    params: status ? { status } : undefined,
  })
  return response.data
}

export async function getApprovalRequest(id) {
  const response = await apiClient.get('/api/approval-requests/' + id)
  return response.data
}

export async function approveApprovalRequest(id, comment) {
  const response = await apiClient.post('/api/approval-requests/' + id + '/approve', { comment: comment || null })
  return response.data
}

export async function rejectApprovalRequest(id, comment) {
  const response = await apiClient.post('/api/approval-requests/' + id + '/reject', { comment: comment || null })
  return response.data
}
