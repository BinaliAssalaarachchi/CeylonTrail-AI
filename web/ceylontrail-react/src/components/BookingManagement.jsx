import { useEffect, useState } from 'react'
import { acceptBooking, getBookingHistory, getProviderBookings, rejectBooking } from '../api/bookings'

export default function BookingManagement() {
    const [bookings, setBookings] = useState([])
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState(null)
    const [statusFilter, setStatusFilter] = useState('ALL')
    const [actionLoadingId, setActionLoadingId] = useState(null)

    // Rejection modal state
    const [rejectingBookingId, setRejectingBookingId] = useState(null)
    const [rejectReason, setRejectReason] = useState('')

    // History modal state
    const [historyBookingId, setHistoryBookingId] = useState(null)
    const [historyList, setHistoryList] = useState([])
    const [historyLoading, setHistoryLoading] = useState(false)

    const fetchBookings = async () => {
        try {
            setLoading(true)
            setError(null)
            const data = await getProviderBookings()
            setBookings(data)
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load bookings.')
        } finally {
            setLoading(false)
        }
    }

    useEffect(() => {
        let isMounted = true
        getProviderBookings()
            .then((data) => {
                if (isMounted) setBookings(data)
            })
            .catch((err) => {
                if (isMounted) setError(err.response?.data?.message || 'Failed to load bookings.')
            })
            .finally(() => {
                if (isMounted) setLoading(false)
            })
        return () => {
            isMounted = false
        }
    }, [])

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

    const handleOpenHistory = async (id) => {
        setHistoryBookingId(id)
        setHistoryLoading(true)
        try {
            const history = await getBookingHistory(id)
            setHistoryList(history)
        } catch (err) {
            alert(err.response?.data?.message || 'Failed to load status history.')
            setHistoryBookingId(null)
        } finally {
            setHistoryLoading(false)
        }
    }

    const filteredBookings = bookings.filter((b) => {
        if (statusFilter === 'ALL') return true
        return b.status?.toUpperCase() === statusFilter
    })

    const pendingCount = bookings.filter((b) => b.status === 'Pending').length
    const confirmedCount = bookings.filter((b) => b.status === 'Confirmed').length

    return (
        <div className="booking-management">
            {/* Header Metrics */}
            <div className="booking-metrics">
                <div className="metric-card">
                    <span className="metric-label">Total Requests</span>
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
            {error && <div className="booking-state-msg error">{error}</div>}

            {!loading && filteredBookings.length === 0 && (
                <div className="booking-empty">
                    <p>No bookings found matching filter "{statusFilter}".</p>
                </div>
            )}

            {/* Bookings List */}
            <div className="booking-grid">
                {filteredBookings.map((b) => (
                    <div key={b.id} className={`booking-card status-${b.status.toLowerCase()}`}>
                        <div className="booking-card-header">
                            <div>
                                <span className={`status-badge ${b.status.toLowerCase()}`}>{b.status}</span>
                                <span className="booking-id">ID: {b.id.substring(0, 8)}...</span>
                            </div>
                            <span className="booking-total">${b.totalAmount.toFixed(2)}</span>
                        </div>

                        <div className="booking-meta">
                            <p><strong>Tourist ID:</strong> {b.touristId.substring(0, 8)}...</p>
                            <p><strong>Requested on:</strong> {new Date(b.createdAt).toLocaleString()}</p>
                        </div>

                        {/* Line items breakdown */}
                        <div className="booking-items-list">
                            <p className="items-title">Items ({b.items.length}):</p>
                            <ul>
                                {b.items.map((item) => (
                                    <li key={item.id}>
                                        <span>Attraction Slot ({item.attractionId.substring(0, 6)}...)</span>
                                        <span>Qty: {item.quantity} × ${item.unitPrice.toFixed(2)} = <strong>${item.subtotal.toFixed(2)}</strong></span>
                                    </li>
                                ))}
                            </ul>
                        </div>

                        {/* Cancellation reason note if cancelled */}
                        {b.cancellation && (
                            <div className="cancellation-alert">
                                <strong>Cancelled:</strong> {b.cancellation.reason}
                            </div>
                        )}

                        {/* Card Actions */}
                        <div className="booking-card-actions">
                            {b.status === 'Pending' && (
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
                            <button
                                className="btn btn-outline"
                                onClick={() => handleOpenHistory(b.id)}
                            >
                                View History
                            </button>
                        </div>
                    </div>
                ))}
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
                                            <p className="timeline-date">{new Date(h.changedAt).toLocaleString()}</p>
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
