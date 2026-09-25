import { useState } from 'react'
import { Link } from 'react-router-dom'
import StatusBadge from './StatusBadge'

function formatPrice(price) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(price)
}

function formatUpdatedDate(value) {
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric' }).format(new Date(value))
}

export default function AttractionCard({ attraction, onDeactivate }) {
  const [menuOpen, setMenuOpen] = useState(false)
  const image = attraction.images?.find((item) => item?.imageUrl)

  return (
    <article className="provider-attraction-card">
      <div className="provider-card-main">
        <div className="provider-card-thumbnail">
          {image ? <img src={image.imageUrl} alt={image.altText || attraction.name} /> : <span aria-hidden="true">✦</span>}
        </div>
        <div className="provider-card-copy">
          <div className="provider-card-topline">
            <p className="eyebrow">{attraction.category?.name || 'Uncategorised'}</p>
            <StatusBadge status={attraction.status} isActive={attraction.isActive} />
          </div>
          <h2>{attraction.name}</h2>
          <p className="provider-card-meta"><span>{attraction.district}</span><span>Price {formatPrice(attraction.price)}</span></p>
          <p className="provider-card-updated">Updated {formatUpdatedDate(attraction.updatedAt)}</p>
          <p className="provider-card-description">{attraction.description}</p>
        </div>
      </div>
      <div className="provider-card-actions">
        <Link className="button button-primary provider-edit-action" to={`/provider/attractions/${attraction.id}/edit`}>Edit</Link>
        <Link className="provider-text-action" to={`/provider/attractions/${attraction.id}/schedules`}>Schedules</Link>
        <Link className="provider-text-action" to={`/provider/attractions/${attraction.id}/slots`}>Slots</Link>
        <Link className="provider-text-action" to={`/provider/attractions/${attraction.id}/availability`}>Availability</Link>
        {attraction.isActive && <div className="provider-card-menu">
          <button className="provider-overflow-button" type="button" aria-label={`More actions for ${attraction.name}`} aria-expanded={menuOpen} onClick={() => setMenuOpen((open) => !open)}>⋯</button>
          {menuOpen && <div className="provider-overflow-menu">
            <button type="button" onClick={() => { setMenuOpen(false); onDeactivate(attraction) }}>Deactivate attraction</button>
          </div>}
        </div>}
      </div>
    </article>
  )
}
