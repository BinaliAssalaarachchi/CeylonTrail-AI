import { useCallback, useEffect, useState } from 'react'
import {
    acceptBooking,
    cancelBooking,
    getBookingHistory,
    getMyBookings,
    getProviderBookings,
    rejectBooking,
} from '../api/bookings'
import { useAuth } from '../context/useAuth'

export default function BookingManagement() {
    const { user } = useAuth()
    const [bookings, setBookings] = useState([])
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState(null)
    const [statusFilter, setStatusFilter] = useState('ALL')
    const [actionLoadingId, setActionLoadingId] = useState(null)

    // Rejection modal state (Provider/Staff)
    const [rejectingBookingId, setRejectingBookingId] = useState(null)
    const [rejectReason, setRejectReason] = useState('')

    // Cancellation modal state (Tourist/Admin)
    const [cancellingBookingId, setCancellingBookingId] = useState(null)
    const [cancelReason, setCancelReason] = useState('')

    // History modal state
    const [historyBookingId, setHistoryBookingId] = useState(null)
    const [historyList, setHistoryList] = useState([])
    const [historyLoading, setHistoryLoading] = useState(false)

    const isStaffOrProvider = ['TourismProvider', 'TravelCoordinator', 'Administrator'].includes(user?.role)

    const fetchBookings = useCallback(async () => {
        try {
            setLoading(true)
            setError(null)
            const fetchFn = isStaffOrProvider ? getProviderBookings : getMyBookings
            const data = await fetchFn()
            setBookings(Array.isArray(data) ? data : [])
        } catch (err) {
            const msg = err.response?.data?.message || err.message || 'Failed to load bookings.'
            setError(msg)
        } finally {
            setLoading(false)
        }
    }, [isStaffOrProvider])

    useEffect(() => {
        fetchBookings()
    }, [fetchBookings])

    const handleAccept = async (id) => {
        try {
            setActionLoadingId(id)
            await acceptBooking(id)
            await fetchBookings()
        } catch (err) {
            alert(err.response?.data?.message || 'Failed to accept booking.')
        } finally {
            setActionLoadingId(null)
        }
    }

    const handleRejectSubmit = async (e) => {
        e.preventDefault()
        if (!rejectReason.trim()) return

        try {
            setActionLoadingId(rejectingBookingId)
            await rejectBooking(rejectingBookingId, rejectReason)
            setRejectingBookingId(null)
            setRejectReason('')
            await fetchBookings()
        } catch (err) {
            alert(err.response?.data?.message || 'Failed to reject booking.')
        } finally {
            setActionLoadingId(null)
        }
    }

    const handleCancelSubmit = async (e) => {
        e.preventDefault()
        if (!cancelReason.trim()) return

        try {
            setActionLoadingId(cancellingBookingId)
            await cancelBooking(cancellingBookingId, cancelReason)
            setCancellingBookingId(null)
            setCancelReason('')
            await fetchBookings()
        } catch (err) {
            alert(err.response?.data?.message || 'Failed to cancel booking.')
        } finally {
            setActionLoadingId(null)
        }
    }

    const handleOpenHistory = async (id) => {
        setHistoryBookingId(id)
        setHistoryLoading(true)
        try {
            const history = await getBookingHistory(id)
            setHistoryList(Array.isArray(history) ? history : [])
        } catch (err) {
            alert(err.response?.data?.message || 'Failed to load status history.')
            setHistoryBookingId(null)
        } finally {
            setHistoryLoading(false)
        }
    }

    const getStatus = (b) => (b.currentStatus || b.status || 'Draft')

    const filteredBookings = bookings.filter((b) => {
        if (statusFilter === 'ALL') return true
        const s = getStatus(b).toUpperCase()
        return s === statusFilter || s.includes(statusFilter)
    })

    const pendingCount = bookings.filter((b) => {
        const s = getStatus(b).toLowerCase()
        return s.includes('pending') || s === 'draft'
    }).length
    const confirmedCount = bookings.filter((b) => getStatus(b).toLowerCase() === 'confirmed').length

    return (
        <div className="booking-management">
            {/* Header Metrics */}
            <div className="booking-metrics">
                <div className="metric-card">
                    <span className="metric-label">{isStaffOrProvider ? 'Total Requests' : 'My Reservations'}</span>
                    <span className="metric-value">{bookings.length}</span>
                </div>
                <div className="metric-card warning">
                    <span className="metric-label">Pending Action</span>
                    <span className="metric-value">{pendingCount}</span>
                </div>
                <div className="metric-card success">
                    <span className="metric-label">Confirmed</span>
                    <span className="metric-value">{confirmedCount}</span>
                </div>
            </div>

            {/* Filter Tabs */}
            <div className="booking-filter-bar">
                {['ALL', 'PENDING', 'CONFIRMED', 'REJECTED', 'CANCELLED'].map((tab) => (
                    <button
                        key={tab}
                        className={`filter-tab ${statusFilter === tab ? 'active' : ''}`}
                        onClick={() => setStatusFilter(tab)}
                    >
                        {tab.charAt(0) + tab.slice(1).toLowerCase()}
                    </button>
                ))}
            </div>

            {/* Loading & Error states */}
            {loading && <div className="booking-state-msg">Loading reservations...</div>}
            {error && (
                <div className="booking-state-msg error">
                    <p>{error}</p>
                    <button className="button button-outline" style={{ marginTop: '0.5rem' }} onClick={fetchBookings}>
                        Retry
                    </button>
                </div>
            )}

            {!loading && !error && filteredBookings.length === 0 && (
                <div className="booking-empty">
                    <p>No bookings found matching filter "{statusFilter}".</p>
                </div>
            )}

            {/* Bookings List */}
            <div className="booking-grid">
                {filteredBookings.map((b) => {
                    const statusStr = getStatus(b)
                    const isPendingState = statusStr.toLowerCase().includes('pending') || statusStr.toLowerCase() === 'draft'
                    const cancellation = b.cancellation || (b.cancellationRequests && b.cancellationRequests[0])
                    const items = b.items || []

                    return (
                        <div key={b.id} className={`booking-card status-${statusStr.toLowerCase()}`}>
                            <div className="booking-card-header">
                                <div>
                                    <span className={`status-badge ${statusStr.toLowerCase()}`}>{statusStr}</span>
                                    <span className="booking-id">ID: {b.id?.substring(0, 8)}...</span>
                                </div>
                                <span className="booking-total">LKR {(b.totalAmount || 0).toFixed(2)}</span>
                            </div>

                            <div className="booking-meta">
                                {isStaffOrProvider && (
                                    <p><strong>User ID:</strong> {(b.userId || b.touristId)?.substring(0, 8)}...</p>
                                )}
                                <p><strong>Date:</strong> {b.createdAt ? new Date(b.createdAt).toLocaleString() : 'N/A'}</p>
                                {b.qrCodeHash && <p><strong>Ticket QR:</strong> {b.qrCodeHash.substring(0, 12)}...</p>}
                            </div>

                            {/* Line items breakdown */}
                            <div className="booking-items-list">
                                <p className="items-title">Items ({items.length}):</p>
                                <ul>
                                    {items.map((item) => {
                                        const guests = item.numberOfGuests ?? item.quantity ?? 1
                                        const unitPrice = item.unitPrice ?? 0
                                        const subtotal = item.subTotal ?? item.subtotal ?? (guests * unitPrice)
                                        const targetId = (item.availabilitySlotId || item.attractionId || item.id || '').substring(0, 6)

                                        return (
                                            <li key={item.id}>
                                                <span>Slot ({targetId}...)</span>
                                                <span>Guests: {guests} × LKR {unitPrice.toFixed(2)} = <strong>LKR {subtotal.toFixed(2)}</strong></span>
                                            </li>
                                        )
                                    })}
                                </ul>
                            </div>



                            {/* Cancellation reason note if cancelled */}
                            {cancellation && (
                                <div className="cancellation-alert">
                                    <strong>Cancellation Note:</strong> {cancellation.reason} ({cancellation.status || 'Processed'})
                                </div>
                            )}

                            {/* Card Actions */}
                            <div className="booking-card-actions">
                                {isStaffOrProvider && isPendingState && (
                                    <>
                                        <button
                                            className="btn btn-primary"
                                            disabled={actionLoadingId === b.id}
                                            onClick={() => handleAccept(b.id)}
                                        >
                                            {actionLoadingId === b.id ? 'Processing...' : 'Accept'}
                                        </button>
                                        <button
                                            className="btn btn-danger"
                                            disabled={actionLoadingId === b.id}
                                            onClick={() => {
                                                setRejectingBookingId(b.id)
                                                setRejectReason('')
                                            }}
                                        >
                                            Reject
                                        </button>
                                    </>
                                )}
                                {(!isStaffOrProvider || user?.role === 'Administrator') && (isPendingState || statusStr.toLowerCase() === 'confirmed') && (
                                    <button
                                        className="btn btn-danger"
                                        disabled={actionLoadingId === b.id}
                                        onClick={() => {
                                            setCancellingBookingId(b.id)
                                            setCancelReason('')
                                        }}
                                    >
                                        Cancel Booking
                                    </button>
                                )}
                                <button
                                    className="btn btn-outline"
                                    onClick={() => handleOpenHistory(b.id)}
                                >
                                    View History
                                </button>
                            </div>
                        </div>
                    )
                })}
            </div>

            {/* Reject Modal */}
            {rejectingBookingId && (
                <div className="modal-overlay" onClick={() => setRejectingBookingId(null)}>
                    <div className="modal-content" onClick={(e) => e.stopPropagation()}>
                        <h3>Reject Reservation Request</h3>
                        <p className="modal-lead">Please provide a reason for declining this reservation:</p>
                        <form onSubmit={handleRejectSubmit}>
                            <textarea
                                required
                                rows={4}
                                value={rejectReason}
                                onChange={(e) => setRejectReason(e.target.value)}
                                placeholder="e.g. Requested time slot is unavailable or venue undergoing maintenance..."
                            />
                            <div className="modal-actions">
                                <button type="button" className="btn btn-outline" onClick={() => setRejectingBookingId(null)}>
                                    Cancel
                                </button>
                                <button type="submit" className="btn btn-danger" disabled={actionLoadingId === rejectingBookingId}>
                                    Confirm Rejection
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Cancel Modal */}
            {cancellingBookingId && (
                <div className="modal-overlay" onClick={() => setCancellingBookingId(null)}>
                    <div className="modal-content" onClick={(e) => e.stopPropagation()}>
                        <h3>Cancel Reservation</h3>
                        <p className="modal-lead">Please specify a reason for cancellation:</p>
                        <form onSubmit={handleCancelSubmit}>
                            <textarea
                                required
                                rows={4}
                                value={cancelReason}
                                onChange={(e) => setCancelReason(e.target.value)}
                                placeholder="e.g. Travel plans changed or rescheduling..."
                            />
                            <div className="modal-actions">
                                <button type="button" className="btn btn-outline" onClick={() => setCancellingBookingId(null)}>
                                    Back
                                </button>
                                <button type="submit" className="btn btn-danger" disabled={actionLoadingId === cancellingBookingId}>
                                    Confirm Cancellation
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* History Audit Modal */}
            {historyBookingId && (
                <div className="modal-overlay" onClick={() => setHistoryBookingId(null)}>
                    <div className="modal-content" onClick={(e) => e.stopPropagation()}>
                        <h3>Status Audit Trail</h3>
                        <p className="modal-lead">Booking ID: {historyBookingId}</p>

                        {historyLoading ? (
                            <p>Loading history...</p>
                        ) : (
                            <div className="history-timeline">
                                {historyList.map((h) => (
                                    <div key={h.id} className="timeline-item">
                                        <div className="timeline-marker" />
                                        <div className="timeline-content">
                                            <div className="timeline-status">
                                                <span className="from-status">{h.previousStatus}</span>
                                                <span className="arrow">➔</span>
                                                <span className="to-status">{h.newStatus}</span>
                                            </div>
                                            <p className="timeline-date">{(h.timestamp || h.changedAt) ? new Date(h.timestamp || h.changedAt).toLocaleString() : 'N/A'}</p>
                                            {h.reason && <p className="timeline-reason">"{h.reason}"</p>}
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}

                        <div className="modal-actions">
                            <button className="btn btn-outline" onClick={() => setHistoryBookingId(null)}>
                                Close
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    )
}
