import { Link } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { getStaffTrips } from '../api/trips'
import TripPlanningStats from '../components/trip-planning/TripPlanningStats'

function formatDate(value) {
  if (!value) return '—'
  const date = /^\d{4}-\d{2}-\d{2}$/.test(value)
    ? new Date(`${value}T00:00:00`)
    : new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : new Intl.DateTimeFormat('en-LK', { dateStyle: 'medium' }).format(date)
}

function statusClass(status = 'Draft') {
  return `status-pill status-${status.toLowerCase()}`
}

export default function TripPlanningOverviewPage() {
  const [trips, setTrips] = useState([])
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    let isCurrent = true
    getStaffTrips()
      .then((data) => {
        if (isCurrent) setTrips(Array.isArray(data) ? data : [])
      })
      .catch(() => {
        if (isCurrent) setTrips([])
      })
      .finally(() => {
        if (isCurrent) setIsLoading(false)
      })

    return () => { isCurrent = false }
  }, [])

  return (
    <section className="workspace-page trip-planning-page" aria-labelledby="trip-planning-title">
      <div className="page-heading">
        <p className="eyebrow">Travel coordination</p>
        <h1 id="trip-planning-title">Trip planning overview</h1>
        <p className="lead">Monitor tourist journeys and review stored itineraries as they move from a draft plan to a completed experience.</p>
      </div>

      {isLoading ? (
        <div className="state-card">Loading trip-planning data…</div>
      ) : (
        <>
          <TripPlanningStats trips={trips} />
          {trips.length === 0 ? (
            <div className="state-card"><strong>No trips to monitor yet.</strong><span>Tourist trip plans will appear here when they are created.</span></div>
          ) : (
            <div className="table-card">
              <div className="section-heading"><div><p className="eyebrow">Live register</p><h2>Tourist trips</h2></div><span className="record-count">{trips.length} records</span></div>
              <div className="table-scroll">
                <table className="trip-table">
                  <thead><tr><th>Trip</th><th>Travel window</th><th>Budget</th><th>Status</th><th>Itinerary</th><th><span className="sr-only">Action</span></th></tr></thead>
                  <tbody>
                    {trips.map((trip) => (
                      <tr key={trip.id}>
                        <td><strong>{trip.name}</strong><small>Tourist {trip.touristId.slice(0, 8)}…</small></td>
                        <td>{formatDate(trip.startDate)}<small>to {formatDate(trip.endDate)}</small></td>
                        <td>LKR {Number(trip.budget).toLocaleString('en-LK')}</td>
                        <td><span className={statusClass(trip.status)}>{trip.status}</span></td>
                        <td><span className={trip.hasItinerary ? 'availability available' : 'availability'}>{trip.hasItinerary ? 'Available' : 'Not generated'}</span></td>
                        <td><Link className="table-link" to={`/trip-planning/${trip.id}`}>Review <span aria-hidden="true">→</span></Link></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}
    </section>
  )
}
