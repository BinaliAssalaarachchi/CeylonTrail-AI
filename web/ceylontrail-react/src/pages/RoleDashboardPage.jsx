import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import BookingManagement from '../components/BookingManagement'
import OperationalAnalytics from '../components/OperationalAnalytics'
import StatusBadge from '../components/attractions/StatusBadge'
import { getMyAttractions } from '../api/attractions'
import { getProviderBookings } from '../api/bookings'
import { useAuth } from '../context/useAuth'
import { primaryAttractionImageFor } from '../utils/attractionImages'

function formatDate(value, options = { day: 'numeric', month: 'short', year: 'numeric' }) {
  if (!value) return ''
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : new Intl.DateTimeFormat(undefined, options).format(date)
}

function formatPrice(value) {
  return `LKR ${Number(value || 0).toLocaleString('en-LK', { maximumFractionDigits: 2 })}`
}

function imageForAttraction(attraction) {
  const mappedImage = primaryAttractionImageFor(attraction.name)
  if (mappedImage) return mappedImage
  const image = attraction.images?.find((item) => item?.isPrimary && item?.imageUrl) || attraction.images?.find((item) => item?.imageUrl)
  return image?.imageUrl || '/images/tea-country-hero.jpg'
}

function ProviderDashboard() {
  const { user } = useAuth()
  const [attractions, setAttractions] = useState([])
  const [bookings, setBookings] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let mounted = true
    Promise.all([getMyAttractions({ page: 1, pageSize: 100 }), getProviderBookings()])
      .then(([attractionResult, bookingResult]) => {
        if (!mounted) return
        setAttractions(attractionResult?.items || [])
        setBookings(Array.isArray(bookingResult) ? bookingResult : [])
      })
      .catch((requestError) => {
        if (mounted) setError(requestError.response?.data?.message || 'Unable to load your provider dashboard.')
      })
      .finally(() => mounted && setLoading(false))
    return () => { mounted = false }
  }, [])

  const slotLookup = useMemo(() => new Map(attractions.flatMap((attraction) => (attraction.experienceSlots || []).map((slot) => [slot.id, { ...slot, attraction }]))), [attractions])
  const confirmedCount = bookings.filter((booking) => (booking.currentStatus || booking.status || '').toLowerCase() === 'confirmed').length
  const publishedCount = attractions.filter((attraction) => attraction.status === 'Approved' && attraction.isActive).length

  const reservations = useMemo(() => bookings
    .slice()
    .sort((first, second) => new Date(second.createdAt || 0) - new Date(first.createdAt || 0))
    .slice(0, 5)
    .map((booking) => {
      const items = booking.items || []
      const linkedItems = items.map((item) => ({ item, slot: slotLookup.get(item.availabilitySlotId) })).filter(({ slot }) => slot)
      const names = [...new Set(linkedItems.map(({ slot }) => slot.attraction.name))]
      const guests = items.reduce((total, item) => total + (item.numberOfGuests ?? item.quantity ?? 0), 0)
      const firstSlot = linkedItems[0]?.slot
      return {
        ...booking,
        experienceName: names.length === 1 ? names[0] : names.length > 1 ? `${names[0]} + ${names.length - 1} more` : 'Experience details unavailable',
        guests,
        date: firstSlot?.date ? `${formatDate(`${firstSlot.date}T00:00:00`)}${firstSlot.startTime ? ` · ${firstSlot.startTime.slice(0, 5)}` : ''}` : '',
      }
    }), [bookings, slotLookup])

  return (
    <section className="provider-ops-dashboard" aria-labelledby="provider-dashboard-title">
      <div className="provider-dashboard-hero"><div><p className="eyebrow eyebrow-on-dark">Tourism provider</p><h1 id="provider-dashboard-title">Welcome back, {user.firstName || 'Provider'}</h1><p>Manage your experiences and keep upcoming reservations on track.</p></div><Link className="button button-primary provider-hero-action" to="/provider/attractions">Manage experiences <span aria-hidden="true">→</span></Link></div>
      {error && <p className="form-error notice-error" role="alert">{error}</p>}

      <div className="provider-summary-grid" aria-label="Provider summary">
        <article className="provider-summary-card"><span className="provider-summary-icon">AT</span><span>My experiences</span><strong>{loading ? '—' : attractions.length}</strong><small>Provider-owned listings</small></article>
        <article className="provider-summary-card"><span className="provider-summary-icon">BK</span><span>Confirmed reservations</span><strong>{loading ? '—' : confirmedCount}</strong><small>Across your experiences</small></article>
        <article className="provider-summary-card"><span className="provider-summary-icon">✓</span><span>Published experiences</span><strong>{loading ? '—' : publishedCount}</strong><small>Approved and active</small></article>
      </div>

      <section className="provider-dashboard-section" aria-labelledby="provider-experiences-title"><div className="provider-dashboard-section-heading"><div><p className="eyebrow">Your catalogue</p><h2 id="provider-experiences-title">My experiences</h2><p>Manage the experiences travellers can discover and book.</p></div><Link className="text-link" to="/provider/attractions">View all experiences <span aria-hidden="true">→</span></Link></div>
        {loading && <div className="provider-dashboard-state">Loading your experiences…</div>}
        {!loading && !error && attractions.length === 0 && <div className="provider-dashboard-empty"><h3>No experiences yet</h3><p>Create your first experience to start building your provider catalogue.</p><Link className="button button-primary" to="/provider/attractions/create">Create an experience</Link></div>}
        {!loading && attractions.length > 0 && <div className="provider-experience-preview">{attractions.slice(0, 4).map((attraction) => <Link className="provider-experience-card" to={`/provider/attractions/${attraction.id}/edit`} key={attraction.id}><div className="provider-experience-image"><img src={imageForAttraction(attraction)} alt="" loading="lazy" onError={(event) => { event.currentTarget.src = '/images/tea-country-hero.jpg' }} /></div><div className="provider-experience-copy"><div className="provider-experience-topline"><span>{attraction.category?.name || 'Experience'}</span><StatusBadge status={attraction.status} /></div><h3>{attraction.name}</h3><p>{attraction.district || 'Sri Lanka'} · {formatPrice(attraction.price)}</p></div></Link>)}</div>}
      </section>

      <section className="provider-dashboard-section" aria-labelledby="provider-reservations-title"><div className="provider-dashboard-section-heading"><div><p className="eyebrow">Booking activity</p><h2 id="provider-reservations-title">Recent reservations</h2><p>Review booking requests and confirmed stays involving your experiences.</p></div><Link className="text-link" to="/bookings">View all bookings <span aria-hidden="true">→</span></Link></div>
        {loading && <div className="provider-dashboard-state">Loading reservations…</div>}
        {!loading && !error && reservations.length === 0 && <div className="provider-dashboard-empty"><h3>No reservations yet</h3><p>Reservations involving your experiences will appear here.</p></div>}
        {!loading && reservations.length > 0 && <div className="provider-reservations-table"><div className="provider-reservation-row provider-reservation-heading"><span>Experience</span><span>Guests</span><span>Amount</span><span>Status</span></div>{reservations.map((booking) => <div className="provider-reservation-row" key={booking.id}><span><strong>{booking.experienceName}</strong><small>{booking.date || `Booked ${formatDate(booking.createdAt)}`}</small></span><span>{booking.guests || '—'}</span><span>{formatPrice(booking.totalAmount)}</span><span><span className={`provider-booking-status provider-booking-status-${(booking.currentStatus || 'unknown').toLowerCase()}`}>{booking.currentStatus || 'Unknown'}</span><small>Reference {booking.id?.slice(0, 8) || '—'}</small></span></div>)}</div>}
      </section>

      <section className="provider-dashboard-actions" aria-labelledby="provider-actions-title"><div><p className="eyebrow">Next steps</p><h2 id="provider-actions-title">Quick actions</h2></div><div><Link className="button button-secondary-light" to="/provider/attractions">Manage experiences <span aria-hidden="true">→</span></Link><Link className="button button-secondary-light" to="/bookings">View reservations <span aria-hidden="true">→</span></Link></div></section>
    </section>
  )
}

export default function RoleDashboardPage({ title, description }) {
  const { user } = useAuth()
  const isCoordinator = user.role === 'TravelCoordinator'
  if (user.role === 'TourismProvider') return <ProviderDashboard />

  return (
    <section className="workspace-page" aria-labelledby="workspace-title">
      <div className="page-heading role-page-heading"><div><p className="eyebrow">{isCoordinator ? 'Journey coordination' : 'Reservation operations'}</p><h1 id="workspace-title">{title}</h1><p className="lead">{description}</p></div>{isCoordinator && <Link className="button button-primary" to="/ai-operations">Open AI review queue</Link>}</div>
      <div className="role-callout"><span className="module-icon">{isCoordinator ? 'AI' : 'OP'}</span><div><strong>{isCoordinator ? 'Review AI-planned journeys and keep traveller plans moving' : 'Monitor CeylonTrail activity and platform operations'}</strong><p>{isCoordinator ? 'Human approval remains the final step for high-impact recommendations.' : 'Live totals and operational detail are sourced from the platform APIs.'}</p></div></div>
      <BookingManagement />
      <OperationalAnalytics />
    </section>
  )
}
