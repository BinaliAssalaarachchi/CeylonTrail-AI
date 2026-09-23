import { Link } from 'react-router-dom'
import StatusBadge from './StatusBadge'

function formatPrice(price) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(price)
}

export default function AttractionCard({ attraction, onDeactivate }) {
  return (
    <article className="management-card">
      <div className="card-heading">
        <div>
          <p className="eyebrow">{attraction.category?.name || 'Uncategorised'}</p>
          <h2>{attraction.name}</h2>
        </div>
        <StatusBadge status={attraction.status} isActive={attraction.isActive} />
      </div>
      <div className="card-meta">
        <span>{attraction.district}</span>
        <span>Price {formatPrice(attraction.price)}</span>
        <span>Updated {new Date(attraction.updatedAt).toLocaleDateString()}</span>
      </div>
      <p className="card-description">{attraction.description}</p>
      <div className="card-actions">
        <Link className="button button-primary" to={`/provider/attractions/${attraction.id}/edit`}>Edit</Link>
        <Link className="button button-secondary dark-button" to={`/provider/attractions/${attraction.id}/schedules`}>Schedules</Link>
        <Link className="button button-secondary dark-button" to={`/provider/attractions/${attraction.id}/slots`}>Slots</Link>
        <Link className="button button-secondary dark-button" to={`/provider/attractions/${attraction.id}/availability`}>Availability</Link>
        {attraction.isActive && <button className="button button-danger" type="button" onClick={() => onDeactivate(attraction)}>Deactivate</button>}
      </div>
    </article>
  )
}
