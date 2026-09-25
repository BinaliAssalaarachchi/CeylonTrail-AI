import apiClient from './client'

export async function getMyAttractions(params = {}) {
  const response = await apiClient.get('/api/attractions/mine', { params })
  return response.data
}

export async function getAttractionById(id) {
  const response = await apiClient.get(`/api/attractions/${id}`)
  return response.data
}

export async function getCategories() {
  const response = await apiClient.get('/api/attractions/categories')
  return response.data
}

export async function getPendingAttractions(params = {}) {
  const response = await apiClient.get('/api/attractions/pending', { params })
  return response.data
}

export async function approveAttraction(id) {
  const response = await apiClient.patch(`/api/attractions/${id}/approve`)
  return response.data
}

export async function createAttraction(data) {
  const response = await apiClient.post('/api/attractions', data)
  return response.data
}

export async function updateAttraction(id, data) {
  const response = await apiClient.put(`/api/attractions/${id}`, data)
  return response.data
}

export async function deleteAttraction(id) {
  await apiClient.delete(`/api/attractions/${id}`)
}

export async function createSchedule(attractionId, data) {
  const response = await apiClient.post(`/api/attractions/${attractionId}/schedules`, data)
  return response.data
}

export async function updateSchedule(attractionId, scheduleId, data) {
  const response = await apiClient.put(`/api/attractions/${attractionId}/schedules/${scheduleId}`, data)
  return response.data
}

export async function deleteSchedule(attractionId, scheduleId) {
  await apiClient.delete(`/api/attractions/${attractionId}/schedules/${scheduleId}`)
}

export async function createExperienceSlot(attractionId, data) {
  const response = await apiClient.post(`/api/attractions/${attractionId}/slots`, data)
  return response.data
}

export async function updateExperienceSlot(attractionId, slotId, data) {
  const response = await apiClient.put(`/api/attractions/${attractionId}/slots/${slotId}`, data)
  return response.data
}

export async function deleteExperienceSlot(attractionId, slotId) {
  await apiClient.delete(`/api/attractions/${attractionId}/slots/${slotId}`)
}

export async function getAvailability(attractionId, date) {
  const response = await apiClient.get(`/api/attractions/${attractionId}/availability`, {
    params: date ? { date } : undefined,
  })
  return response.data
}

export async function addAttractionImage(attractionId, data) {
  const response = await apiClient.post(`/api/attractions/${attractionId}/images`, data)
  return response.data
}

export async function deleteAttractionImage(attractionId, imageId) {
  await apiClient.delete(`/api/attractions/${attractionId}/images/${imageId}`)
}

export async function setPrimaryAttractionImage(attractionId, imageId) {
  const response = await apiClient.patch(`/api/attractions/${attractionId}/images/${imageId}/primary`)
  return response.data
}
