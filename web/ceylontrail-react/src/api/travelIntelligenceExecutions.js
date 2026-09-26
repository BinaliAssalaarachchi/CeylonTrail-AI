import apiClient from './client'

export async function getTravelIntelligenceExecutions({ page = 1, pageSize = 20, executionStatus, usedFallback } = {}) {
  const params = { page, pageSize }
  if (executionStatus) params.executionStatus = executionStatus
  if (usedFallback !== undefined && usedFallback !== '') params.usedFallback = usedFallback
  const response = await apiClient.get('/api/travel-intelligence/executions', { params })
  return response.data
}

export async function getTravelIntelligenceExecution(id) {
  const response = await apiClient.get('/api/travel-intelligence/executions/' + id)
  return response.data
}

export async function getAgentWorkflow(workflowId) {
  const response = await apiClient.get('/api/agent-workflows/' + workflowId)
  return response.data
}
