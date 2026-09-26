import { useState } from 'react'
import { Link } from 'react-router-dom'
import StatusBadge from './StatusBadge'
import AttractionPreviewModal from './AttractionPreviewModal'

function formatPrice(price) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(price)
}

function formatUpdatedDate(value) {
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric' }).format(new Date(value))
}

export default function AttractionCard({ attraction, onDelete }) {
  const [menuOpen, setMenuOpen] = useState(false)
  const [previewOpen, setPreviewOpen] = useState(false)
  const image =
    attraction.images?.find((item) => item?.isPrimary && item?.imageUrl) ||
    attraction.images?.find((item) => item?.imageUrl)

  const isRejected = attraction.status === 'Rejected'

  return (
    <article className={`provider-attraction-card ${isRejected ? 'card-rejected-state' : ''}`}>
      <div className="provider-card-main">
        <div className="provider-card-thumbnail">
          {image ? (
            <button
              className="provider-card-image-button"
              type="button"
              onClick={() => setPreviewOpen(true)}
              aria-label={`Preview ${attraction.name}`}
            >
              <img src={image.imageUrl} alt={image.altText || attraction.name} />
            </button>
          ) : (
            <span aria-hidden="true">✦</span>
          )}
        </div>
        <div className="provider-card-copy">
          <div className="provider-card-topline">
            <p className="eyebrow">{attraction.category?.name || 'Uncategorised'}</p>
            <StatusBadge status={attraction.status} />
          </div>
          <h2>{attraction.name}</h2>
          <p className="provider-card-meta">
            <span>{attraction.district}</span>
            <span>Price {formatPrice(attraction.price)}</span>
          </p>
          <p className="provider-card-updated">Updated {formatUpdatedDate(attraction.updatedAt)}</p>
          <p className="provider-card-description">{attraction.description}</p>

          {/* Rejection Notification Banner */}
          {isRejected && attraction.rejectionReason && (
            <div className="provider-rejection-callout" role="alert">
              <strong>⚠️ Administrator Feedback:</strong>
              <p>{attraction.rejectionReason}</p>
              <small>Click "Edit & Resubmit" to make changes and submit for review again.</small>
            </div>
          )}
        </div>
      </div>

      <div className="provider-card-actions">
        <Link
          className={`button ${isRejected ? 'button-danger' : 'button-primary'} provider-edit-action`}
          to={`/provider/attractions/${attraction.id}/edit`}
        >
          {isRejected ? 'Edit & Resubmit' : 'Edit'}
        </Link>
        <Link className="provider-text-action" to={`/provider/attractions/${attraction.id}/schedules`}>
          Schedules
        </Link>
        <Link className="provider-text-action" to={`/provider/attractions/${attraction.id}/slots`}>
          Slots
        </Link>
        <Link className="provider-text-action" to={`/provider/attractions/${attraction.id}/availability`}>
          Availability
        </Link>
        <div className="provider-card-menu">
          <button
            className="provider-overflow-button"
            type="button"
            aria-label={`More actions for ${attraction.name}`}
            aria-expanded={menuOpen}
            onClick={() => setMenuOpen((open) => !open)}
          >
            ⋯
          </button>
          {menuOpen && (
            <div className="provider-overflow-menu">
              <button
                type="button"
                style={{ color: '#dc2626' }}
                onClick={() => {
                  setMenuOpen(false)
                  onDelete(attraction)
                }}
              >
                Delete attraction permanently
              </button>
            </div>
          )}
        </div>
      </div>
      {previewOpen && <AttractionPreviewModal attraction={attraction} onClose={() => setPreviewOpen(false)} canEdit />}
    </article>
  )
}
