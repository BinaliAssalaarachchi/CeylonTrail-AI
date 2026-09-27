import apiClient from './client'

export async function getProviderBookings() {
    const response = await apiClient.get('/api/provider/bookings')
    return response.data
}

export async function getMyBookings() {
    const response = await apiClient.get('/api/bookings')
    return response.data
}

export async function createBooking(data) {
    const response = await apiClient.post('/api/bookings', data)
    return response.data
}

export async function acceptBooking(bookingId) {
    const response = await apiClient.post(`/api/bookings/${bookingId}/accept`)
    return response.data
}

export async function rejectBooking(bookingId, reason) {
    const response = await apiClient.post(`/api/bookings/${bookingId}/reject`, { reason })
    return response.data
}

export async function cancelBooking(bookingId, reason) {
    const response = await apiClient.post(`/api/bookings/${bookingId}/cancel`, { reason })
    return response.data
}

export async function getBookingHistory(bookingId) {
    const response = await apiClient.get(`/api/bookings/${bookingId}/history`)
    return response.data
}

export async function deleteBooking(bookingId) {
    const response = await apiClient.delete(`/api/bookings/${bookingId}`)
    return response.data
}

