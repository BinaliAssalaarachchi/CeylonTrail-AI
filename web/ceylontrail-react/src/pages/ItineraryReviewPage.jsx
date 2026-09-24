import { Link, useParams } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { getStaffTrip, getStaffTripItinerary, getStaffTripItineraryHistory, getStaffTripItineraryVersion } from '../api/trips'
import ItineraryReview from '../components/trip-planning/ItineraryReview'

function formatDate(value) {
  const date = /^\d{4}-\d{2}-\d{2}$/.test(value)
    ? new Date(`${value}T00:00:00`)
    : new Date(value)
  return new Intl.DateTimeFormat('en-LK', { dateStyle: 'medium' }).format(date)
}

export default function ItineraryReviewPage() {
  const { id } = useParams()
  const [trip, setTrip] = useState(null)
  const [itinerary, setItinerary] = useState(null)
  const [history, setHistory] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let isCurrent = true
    async function loadReview() {
      try {
        const tripData = await getStaffTrip(id)
        if (!isCurrent) return
        setTrip(tripData)
        try {
          setItinerary(await getStaffTripItinerary(id))
        } catch (requestError) {
          if (requestError.response?.status !== 404) throw requestError
          setItinerary(null)
        }
        setHistory(await getStaffTripItineraryHistory(id))
      } catch {
        if (isCurrent) setError('This trip could not be loaded or is no longer available.')
      } finally {
        if (isCurrent) setIsLoading(false)
      }
    }
    loadReview()
    return () => { isCurrent = false }
  }, [id])

  if (isLoading) return <section className="workspace-page"><div className="state-card">Loading itinerary review…</div></section>
  if (error) return <section className="workspace-page"><div className="state-card state-error" role="alert">{error}</div><Link className="button button-primary back-button" to="/trip-planning">Back to overview</Link></section>

  return (
    <section className="workspace-page trip-planning-page" aria-labelledby="review-title">
      <Link className="back-link" to="/trip-planning">← Back to trip overview</Link>
      <div className="page-heading review-heading">
        <p className="eyebrow">Read-only review</p>
        <h1 id="review-title">{trip.name}</h1>
        <p className="lead">{formatDate(trip.startDate)} to {formatDate(trip.endDate)} · <span className="status-pill status-inline">{trip.status}</span></p>
      </div>
      {!itinerary ? (
        <div className="state-card state-empty"><strong>No generated itinerary is available for this trip yet.</strong><span>This screen will update when an itinerary is stored by the planning workflow.</span></div>
      ) : (
        <>
          <ItineraryReview itinerary={itinerary} />
          <section className="itinerary-history" aria-labelledby="history-title">
            <h2 id="history-title">Previous versions</h2>
            {history.length <= 1 ? <p className="muted">No previous itinerary versions are available.</p> : history.slice(1).map((version) => (
              <button className="history-row" key={version.id} onClick={async () => setItinerary(await getStaffTripItineraryVersion(id, version.id))}>
                <span>{formatDate(version.createdAt)}</span>
                <span className="status-pill">{version.status}</span>
                <span>Rs. {Number(version.totalEstimatedCost).toLocaleString('en-LK')}</span>
              </button>
            ))}
          </section>
        </>
      )}
    </section>
  )
}
