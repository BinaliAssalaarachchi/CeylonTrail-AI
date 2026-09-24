import apiClient from './client'

export async function getStaffTrips() {
  const response = await apiClient.get('/api/staff/trips')
  return response.data
}

export async function getStaffTrip(id) {
  const response = await apiClient.get(`/api/staff/trips/${id}`)
  return response.data
}

export async function getStaffTripItinerary(id) {
  const response = await apiClient.get(`/api/staff/trips/${id}/itinerary`)
  return response.data
}

