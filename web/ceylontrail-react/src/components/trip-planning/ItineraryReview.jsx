function formatDate(value) {
  const date = /^\d{4}-\d{2}-\d{2}$/.test(value)
    ? new Date(`${value}T00:00:00`)
    : new Date(value)
  return new Intl.DateTimeFormat('en-LK', { dateStyle: 'medium' }).format(date)
}

function formatTime(value) {
  return value?.slice(0, 5) ?? '—'
}

function statusClass(status = '') {
  return `status-pill status-${status.toLowerCase()}`
}

export default function ItineraryReview({ itinerary }) {
  return (
    <div className="itinerary-review">
      <div className="itinerary-summary-grid">
        <div><span>Status</span><strong className={statusClass(itinerary.status)}>{itinerary.status}</strong></div>
        <div><span>Estimated cost</span><strong>LKR {Number(itinerary.totalEstimatedCost).toLocaleString('en-LK')}</strong></div>
        <div><span>Generated</span><strong>{formatDate(itinerary.createdAt)}</strong></div>
        <div><span>Last updated</span><strong>{formatDate(itinerary.updatedAt)}</strong></div>
      </div>

      <div className="itinerary-days">
        {itinerary.days?.map((day) => (
          <article className="itinerary-day" key={day.id}>
            <div className="itinerary-day-heading">
              <span className="day-number">Day {day.dayNumber}</span>
              <strong>{formatDate(day.date)}</strong>
            </div>
            {day.items?.length ? (
              <div className="itinerary-items">
                {day.items.map((item) => (
                  <div className="itinerary-item" key={item.id}>
                    <div className="itinerary-item-time">{formatTime(item.startTime)}–{formatTime(item.endTime)}</div>
                    <div className="itinerary-item-copy">
                      <strong>Attraction reference</strong>
                      <span>{item.attractionId}</span>
                      {item.notes && <p>{item.notes}</p>}
                    </div>
                    <span className="itinerary-item-cost">LKR {Number(item.estimatedCost).toLocaleString('en-LK')}</span>
                  </div>
                ))}
              </div>
            ) : (
              <p className="muted itinerary-empty-day">No activities are stored for this day.</p>
            )}
          </article>
        ))}
      </div>
    </div>
  )
}
