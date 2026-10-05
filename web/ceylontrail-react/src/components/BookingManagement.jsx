import { useCallback, useEffect, useMemo, useState } from 'react'
import { acceptBooking, cancelBooking, getBookingHistory, getMyBookings, getProviderBookings, rejectBooking } from '../api/bookings'
import { getMyAttractions } from '../api/attractions'
import { primaryAttractionImageFor } from '../utils/attractionImages'
import { useAuth } from '../context/useAuth'

const getStatus = (booking) => booking.currentStatus || booking.status || 'Draft'
const statusLabel = (status) => status.replace(/([a-z])([A-Z])/g, '$1 $2')
const formatDate = (value) => {
  if (!value) return ''
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? ''
    : new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short', year: 'numeric' }).format(date)
}
const formatPrice = (value) => `LKR ${Number(value || 0).toLocaleString('en-LK', { maximumFractionDigits: 2 })}`

function destinationImageFor(name, explicitImage) {
  return explicitImage || primaryAttractionImageFor(name) || '/images/tea-country-hero.jpg'
}

export default function BookingManagement() {
  const { user } = useAuth()
  const [bookings, setBookings] = useState([])
  const [attractions, setAttractions] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [statusFilter, setStatusFilter] = useState('ALL')
  const [search, setSearch] = useState('')
  const [actionLoadingId, setActionLoadingId] = useState(null)
  const [rejectingBookingId, setRejectingBookingId] = useState(null)
  const [rejectReason, setRejectReason] = useState('')
  const [cancellingBookingId, setCancellingBookingId] = useState(null)
  const [cancelReason, setCancelReason] = useState('')
  const [historyBookingId, setHistoryBookingId] = useState(null)
  const [historyList, setHistoryList] = useState([])
  const [historyLoading, setHistoryLoading] = useState(false)

  const isStaffOrProvider = ['TourismProvider', 'TravelCoordinator', 'Administrator'].includes(user?.role)

  const fetchBookings = useCallback(async () => {
    try {
      setLoading(true)
      setError('')
      const data = await (isStaffOrProvider ? getProviderBookings() : getMyBookings())
      setBookings(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(requestError.response?.data?.message || requestError.message || 'Failed to load bookings.')
    } finally {
      setLoading(false)
    }
  }, [isStaffOrProvider])

  useEffect(() => {
    fetchBookings()
  }, [fetchBookings])

  useEffect(() => {
    if (user?.role !== 'TourismProvider') return undefined
    let mounted = true
    getMyAttractions({ page: 1, pageSize: 100 })
      .then((result) => mounted && setAttractions(result?.items || []))
      .catch(() => {})
    return () => {
      mounted = false
    }
  }, [user?.role])

  const slotLookup = useMemo(
    () => new Map(attractions.flatMap((attraction) => (attraction.experienceSlots || []).map((slot) => [slot.id, { ...slot, attraction }]))),
    [attractions]
  )

  const details = useMemo(() => {
    return bookings.map((booking) => {
      const items = booking.items || []
      const names = [
        ...new Set(
          items
            .map((item) => item.attractionName || slotLookup.get(item.availabilitySlotId)?.attraction?.name)
            .filter(Boolean)
        )
      ]
      const name = names.length === 1 ? names[0] : names.length > 1 ? `${names[0]} + ${names.length - 1} more` : (items[0]?.attractionName || 'Reservation')
      const firstItem = items[0]
      const firstSlot = slotLookup.get(firstItem?.availabilitySlotId)

      let date = ''
      if (firstItem?.slotStartTime) {
        date = `${formatDate(firstItem.slotStartTime)} · ${new Date(firstItem.slotStartTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`
      } else if (firstSlot?.date) {
        date = `${formatDate(`${firstSlot.date}T00:00:00`)}${firstSlot.startTime ? ` · ${firstSlot.startTime.slice(0, 5)}` : ''}`
      } else {
        date = `Booked ${formatDate(booking.createdAt)}`
      }

      const image = destinationImageFor(name, firstItem?.attractionImageUrl)
      const activeAdvisories = Array.isArray(booking.activeAdvisories) ? booking.activeAdvisories : []
      const searchText = `${name} ${booking.id || ''} ${getStatus(booking)} ${items.map((i) => i.district || '').join(' ')}`.toLowerCase()

      return { booking, items, name, date, image, activeAdvisories, searchText }
    })
  }, [bookings, slotLookup])

  const statusOptions = useMemo(
    () => ['ALL', ...new Set(bookings.map((booking) => getStatus(booking).toUpperCase()))],
    [bookings]
  )

  const filteredDetails = details.filter(({ booking, searchText }) => {
    const selected = statusFilter === 'ALL' || getStatus(booking).toUpperCase() === statusFilter
    return selected && (!search.trim() || searchText.includes(search.trim().toLowerCase()))
  })

  const pendingCount = bookings.filter((booking) => {
    const status = getStatus(booking).toLowerCase()
    return status.includes('pending') || status === 'draft'
  }).length

  const confirmedCount = bookings.filter((booking) => getStatus(booking).toLowerCase() === 'confirmed').length

  async function handleAccept(id) {
    try {
      setActionLoadingId(id)
      await acceptBooking(id)
      await fetchBookings()
    } catch (requestError) {
      alert(requestError.response?.data?.message || 'Failed to accept booking.')
    } finally {
      setActionLoadingId(null)
    }
  }

  async function handleRejectSubmit(event) {
    event.preventDefault()
    if (!rejectReason.trim()) return
    try {
      setActionLoadingId(rejectingBookingId)
      await rejectBooking(rejectingBookingId, rejectReason)
      setRejectingBookingId(null)
      setRejectReason('')
      await fetchBookings()
    } catch (requestError) {
      alert(requestError.response?.data?.message || 'Failed to reject booking.')
    } finally {
      setActionLoadingId(null)
    }
  }

  async function handleCancelSubmit(event) {
    event.preventDefault()
    if (!cancelReason.trim()) return
    try {
      setActionLoadingId(cancellingBookingId)
      await cancelBooking(cancellingBookingId, cancelReason)
      setCancellingBookingId(null)
      setCancelReason('')
      await fetchBookings()
    } catch (requestError) {
      alert(requestError.response?.data?.message || 'Failed to cancel booking.')
    } finally {
      setActionLoadingId(null)
    }
  }

  async function handleOpenHistory(id) {
    setHistoryBookingId(id)
    setHistoryLoading(true)
    try {
      const history = await getBookingHistory(id)
      setHistoryList(Array.isArray(history) ? history : [])
    } catch (requestError) {
      alert(requestError.response?.data?.message || 'Failed to load status history.')
      setHistoryBookingId(null)
    } finally {
      setHistoryLoading(false)
    }
  }

  return (
    <div className="booking-management provider-booking-management">
      <div className="booking-metrics">
        <div className="metric-card">
          <span className="metric-label">Total reservations</span>
          <span className="metric-value">{bookings.length}</span>
        </div>
        <div className="metric-card warning">
          <span className="metric-label">Pending</span>
          <span className="metric-value">{pendingCount}</span>
        </div>
        <div className="metric-card success">
          <span className="metric-label">Confirmed</span>
          <span className="metric-value">{confirmedCount}</span>
        </div>
      </div>

      <div className="booking-filter-bar provider-booking-filters">
        <input
          type="search"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Search experiences or references"
          aria-label="Search reservations"
        />
        {statusOptions.map((status) => (
          <button
            key={status}
            className={`filter-tab ${statusFilter === status ? 'active' : ''}`}
            type="button"
            onClick={() => setStatusFilter(status)}
          >
            {status === 'ALL' ? 'All' : statusLabel(status.charAt(0) + status.slice(1).toLowerCase())}
          </button>
        ))}
      </div>

      {loading && <div className="booking-state-msg">Loading reservations…</div>}
      {error && (
        <div className="booking-state-msg error">
          <p>{error}</p>
          <button className="button button-outline" type="button" onClick={fetchBookings}>
            Retry
          </button>
        </div>
      )}
      {!loading && !error && filteredDetails.length === 0 && (
        <div className="booking-empty">
          <p>No reservations match the current filter.</p>
        </div>
      )}

      <div className="booking-grid">
        {filteredDetails.map(({ booking, items, name, date, image, activeAdvisories }) => {
          const status = getStatus(booking)
          const pending = status.toLowerCase().includes('pending') || status.toLowerCase() === 'draft'
          const cancellation = booking.cancellation || booking.cancellationRequests?.[0]
          const guests = items.reduce((total, item) => total + (item.numberOfGuests ?? item.quantity ?? 0), 0)

          return (
            <article key={booking.id} className={`booking-card status-${status.toLowerCase()}`}>
              <div className="provider-booking-card-identity">
                <img
                  src={image}
                  alt={name}
                  loading="lazy"
                  onError={(event) => {
                    event.currentTarget.src = '/images/tea-country-hero.jpg'
                  }}
                />
                <div>
                  <h3>{name}</h3>
                  <p>{date}</p>
                  <span className={`status-badge ${status.toLowerCase()}`}>{statusLabel(status)}</span>
                </div>
              </div>

              <div className="booking-card-header">
                <div>
                  <span className="booking-meta-label">Guests</span>
                  <strong>{guests || '—'}</strong>
                </div>
                <div>
                  <span className="booking-meta-label">Total</span>
                  <strong>{formatPrice(booking.totalAmount)}</strong>
                </div>
              </div>

              <div className="booking-items-list">
                <p className="items-title">Reservation details</p>
                <ul>
                  {items.map((item) => {
                    const itemGuests = item.numberOfGuests ?? item.quantity ?? 1
                    const subtotal = item.subTotal ?? item.subtotal ?? itemGuests * (item.unitPrice ?? 0)
                    const itemName = item.attractionName || slotLookup.get(item.availabilitySlotId)?.attraction?.name || name
                    return (
                      <li key={item.id || item.availabilitySlotId}>
                        <span>{itemName}{item.district ? ` (${item.district})` : ''}</span>
                        <span>
                          {itemGuests} guest{itemGuests === 1 ? '' : 's'} · {formatPrice(subtotal)}
                        </span>
                      </li>
                    )
                  })}
                </ul>
              </div>

              {activeAdvisories.length > 0 && (
                <div className="booking-advisories-alert">
                  <div className="advisories-alert-header">
                    <span className="advisories-alert-icon" aria-hidden="true">⚠️</span>
                    <strong>Travel Advisory on Booking Date ({activeAdvisories.length})</strong>
                  </div>
                  <div className="advisories-list">
                    {activeAdvisories.map((advisory) => (
                      <div
                        key={advisory.id || advisory.title}
                        className={`advisory-badge-card severity-${(advisory.severity || 'warning').toLowerCase()}`}
                      >
                        <div className="advisory-top-row">
                          <span className={`advisory-severity-pill severity-${(advisory.severity || 'warning').toLowerCase()}`}>
                            {advisory.severity || 'Advisory'}
                          </span>
                          {advisory.district && <span className="advisory-district-tag">📍 {advisory.district}</span>}
                        </div>
                        <p className="advisory-title">{advisory.title}</p>
                        {advisory.description && (
                          <p className="advisory-description">{advisory.description}</p>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {cancellation && (
                <div className="cancellation-alert">
                  <strong>Cancellation note:</strong> {cancellation.reason} ({cancellation.status || 'Processed'})
                </div>
              )}

              <div className="booking-card-actions">
                {isStaffOrProvider && pending && (
                  <>
                    <button
                      className="btn btn-primary"
                      type="button"
                      disabled={actionLoadingId === booking.id}
                      onClick={() => handleAccept(booking.id)}
                    >
                      {actionLoadingId === booking.id ? 'Processing…' : 'Accept'}
                    </button>
                    <button
                      className="btn btn-danger"
                      type="button"
                      disabled={actionLoadingId === booking.id}
                      onClick={() => {
                        setRejectingBookingId(booking.id)
                        setRejectReason('')
                      }}
                    >
                      Reject
                    </button>
                  </>
                )}
                {(!isStaffOrProvider || user?.role === 'Administrator') &&
                  (pending || status.toLowerCase() === 'confirmed') && (
                    <button
                      className="btn btn-danger"
                      type="button"
                      disabled={actionLoadingId === booking.id}
                      onClick={() => {
                        setCancellingBookingId(booking.id)
                        setCancelReason('')
                      }}
                    >
                      Cancel booking
                    </button>
                  )}
                <button
                  className="btn btn-outline"
                  type="button"
                  onClick={() => handleOpenHistory(booking.id)}
                >
                  View reservation history
                </button>
              </div>
              <small className="booking-reference">Reference: {booking.id?.slice(0, 8) || '—'}…</small>
            </article>
          )
        })}
      </div>

      {rejectingBookingId && (
        <div className="modal-overlay" onClick={() => setRejectingBookingId(null)}>
          <div className="modal-content" onClick={(event) => event.stopPropagation()}>
            <h3>Reject reservation request</h3>
            <p className="modal-lead">Please provide a reason for declining this reservation:</p>
            <form onSubmit={handleRejectSubmit}>
              <textarea
                required
                rows={4}
                value={rejectReason}
                onChange={(event) => setRejectReason(event.target.value)}
                placeholder="Reason for declining this reservation"
              />
              <div className="modal-actions">
                <button type="button" className="btn btn-outline" onClick={() => setRejectingBookingId(null)}>
                  Cancel
                </button>
                <button type="submit" className="btn btn-danger" disabled={actionLoadingId === rejectingBookingId}>
                  Confirm rejection
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {cancellingBookingId && (
        <div className="modal-overlay" onClick={() => setCancellingBookingId(null)}>
          <div className="modal-content" onClick={(event) => event.stopPropagation()}>
            <h3>Cancel reservation</h3>
            <p className="modal-lead">Please specify a reason for cancellation:</p>
            <form onSubmit={handleCancelSubmit}>
              <textarea
                required
                rows={4}
                value={cancelReason}
                onChange={(event) => setCancelReason(event.target.value)}
                placeholder="Reason for cancellation"
              />
              <div className="modal-actions">
                <button type="button" className="btn btn-outline" onClick={() => setCancellingBookingId(null)}>
                  Back
                </button>
                <button type="submit" className="btn btn-danger" disabled={actionLoadingId === cancellingBookingId}>
                  Confirm cancellation
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {historyBookingId && (
        <div className="modal-overlay" onClick={() => setHistoryBookingId(null)}>
          <div className="modal-content" onClick={(event) => event.stopPropagation()}>
            <h3>Reservation details</h3>
            <p className="modal-lead">Reference: {historyBookingId}</p>
            {historyLoading ? (
              <p>Loading status history…</p>
            ) : (
              <div className="history-timeline">
                {historyList.map((history) => (
                  <div className="timeline-item" key={history.id}>
                    <div className="timeline-marker" />
                    <div className="timeline-content">
                      <div className="timeline-status">
                        <span>{history.previousStatus}</span>
                        <span className="arrow">→</span>
                        <span>{history.newStatus}</span>
                      </div>
                      <p className="timeline-date">{formatDate(history.timestamp || history.changedAt)}</p>
                      {history.reason && <p className="timeline-reason">“{history.reason}”</p>}
                    </div>
                  </div>
                ))}
              </div>
            )}
            <div className="modal-actions">
              <button className="btn btn-outline" type="button" onClick={() => setHistoryBookingId(null)}>
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
