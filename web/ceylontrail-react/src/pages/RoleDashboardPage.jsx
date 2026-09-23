import BookingManagement from '../components/BookingManagement'

export default function RoleDashboardPage({ title, description }) {
  return (
    <section className="workspace-page" aria-labelledby="workspace-title">
      <div className="page-heading">
        <p className="eyebrow">Reservation Operations</p>
        <h1 id="workspace-title">{title}</h1>
        <p className="lead">{description}</p>
      </div>

      {/* Live Provider / Staff Booking Management Workspace */}
      <BookingManagement />
    </section>
  )
}
