export default function TripPlanningStats({ trips }) {
  const stats = [
    { label: 'Total trips', value: trips.length, tone: 'forest' },
    { label: 'Draft', value: trips.filter((trip) => trip.status === 'Draft').length, tone: 'amber' },
    { label: 'Planned', value: trips.filter((trip) => trip.status === 'Planned').length, tone: 'teal' },
    { label: 'Completed', value: trips.filter((trip) => trip.status === 'Completed').length, tone: 'mint' },
    { label: 'With itinerary', value: trips.filter((trip) => trip.hasItinerary).length, tone: 'teal' },
    { label: 'Awaiting itinerary', value: trips.filter((trip) => !trip.hasItinerary).length, tone: 'sand' },
  ]

  return (
    <div className="trip-stats-grid" aria-label="Trip planning statistics">
      {stats.map((stat) => (
        <article className={`trip-stat-card trip-stat-${stat.tone}`} key={stat.label}>
          <span>{stat.label}</span>
          <strong>{stat.value}</strong>
        </article>
      ))}
    </div>
  )
}
