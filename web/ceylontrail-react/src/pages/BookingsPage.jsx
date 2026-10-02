import BookingManagement from '../components/BookingManagement'

export default function BookingsPage() {
  return <section className="page-section wide-page bookings-page" aria-labelledby="bookings-title"><div className="consistent-page-header"><p className="eyebrow">Booking management</p><h1 id="bookings-title">Reservations</h1><p className="lead">Keep track of traveller reservations across your experiences.</p></div><BookingManagement /></section>
}
