import BookingManagement from '../components/BookingManagement'
import { useAuth } from '../context/useAuth'

export default function RoleDashboardPage({ title, description }) {
  const { user } = useAuth()
  const isTourist = user?.role === 'Tourist'

  const pageTitle = isTourist ? 'My Bookings & Reservations' : (title || 'Reservation & Booking Operations')
  const pageDescription = isTourist
    ? 'View your confirmed and pending bookings, manage cancellations, or track status histories.'
    : (description || 'Manage incoming reservations, accept or reject requests, and track status histories.')

  return (
    <section className="workspace-page" aria-labelledby="workspace-title">
      <div className="page-heading">
        <p className="eyebrow">{isTourist ? 'Tourist Reservations' : 'Reservation Operations'}</p>
        <h1 id="workspace-title">{pageTitle}</h1>
        <p className="lead">{pageDescription}</p>
      </div>

      {/* Live Booking Management Workspace */}
      <BookingManagement />
    </section>
  )
}
