import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { approveAttraction, getAttractionById, rejectAttraction, setAttractionStatus } from '../../api/attractions'
import StatusBadge from '../../components/attractions/StatusBadge'
import { apiErrorMessage, timeValue } from '../attractions/attractionUtils'

function DetailSection({ label, children }) {
  return (
    <section className="review-section">
      <p className="eyebrow">{label}</p>
      {children}
    </section>
  )
}

function formatDate(value) {
  if (!value) return 'N/A'
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

export default function AdminAttractionDetailsPage() {
  const { id } = useParams()
  const [attraction, setAttraction] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState('')

  // Rejection box state
  const [showRejectForm, setShowRejectForm] = useState(false)
  const [rejectReason, setRejectReason] = useState('')

  useEffect(() => {
    getAttractionById(id)
      .then((data) => {
        setAttraction(data)
        if (data.rejectionReason) {
          setRejectReason(data.rejectionReason)
        }
      })
      .catch((e) => setError(apiErrorMessage(e, 'Unable to load attraction.')))
      .finally(() => setLoading(false))
  }, [id])

  async function handleApprove() {
    setSaving(true)
    setError('')
    try {
      const updated = await approveAttraction(id)
      setAttraction(updated)
      setShowRejectForm(false)
      setMessage('Attraction has been approved and is now discoverable.')
    } catch (e) {
      setError(apiErrorMessage(e, 'Unable to approve attraction.'))
    } finally {
      setSaving(false)
    }
  }

  async function handleReject(e) {
    e?.preventDefault()
    if (!rejectReason.trim()) {
      setError('Please provide a rejection reason.')
      return
    }

    setSaving(true)
    setError('')
    try {
      const updated = await rejectAttraction(id, rejectReason.trim())
      setAttraction(updated)
      setShowRejectForm(false)
      setMessage('Attraction was rejected and the provider has been notified with the reason.')
    } catch (e) {
      setError(apiErrorMessage(e, 'Unable to reject attraction.'))
    } finally {
      setSaving(false)
    }
  }

  async function handleStatusChange(newStatus) {
    if (newStatus === 'Rejected') {
      setShowRejectForm(true)
      return
    }

    setSaving(true)
    setError('')
    try {
      const updated = await setAttractionStatus(id, newStatus)
      setAttraction(updated)
      setShowRejectForm(false)
      setMessage(`Attraction status updated to “${newStatus}”.`)
    } catch (e) {
      setError(apiErrorMessage(e, 'Unable to update status.'))
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return (
      <section className="page-section">
        <div className="state-card" role="status">
          <strong>Loading attraction details…</strong>
          <span>Preparing the administrator review dossier.</span>
        </div>
      </section>
    )
  }

  if (error && !attraction) {
    return (
      <section className="page-section">
        <div className="state-card state-error" role="alert">
          <strong>{error}</strong>
          <span>We could not load this attraction.</span>
        </div>
        <Link className="button button-secondary-light" to="/admin/attractions">
          Back to all attractions
        </Link>
      </section>
    )
  }

  const primaryImage =
    attraction.images?.find((img) => img.isPrimary && img.imageUrl) ||
    attraction.images?.[0]

  return (
    <section className="page-section wide-page review-page">
      <Link className="back-link" to="/admin/attractions">
        ← Back to attractions management
      </Link>

      <div className="page-header consistent-page-header">
        <div>
          <p className="eyebrow">Administrator Review & Decision</p>
          <h1>{attraction.name}</h1>
          <p className="lead">
            Inspect all provider-submitted details, images, schedules, and pricing before publishing or rejecting.
          </p>
        </div>
        <StatusBadge status={attraction.status} isActive={attraction.isActive} />
      </div>

      {message && <p className="success-message" role="status">{message}</p>}
      {error && <p className="form-error notice-error" role="alert">{error}</p>}

      {/* Rejection Alert Banner if currently rejected */}
      {attraction.status === 'Rejected' && attraction.rejectionReason && (
        <div className="admin-rejection-banner" role="alert">
          <div className="rejection-banner-header">
            <strong>⚠️ Currently Marked as Rejected</strong>
            <button
              type="button"
              className="button button-secondary-light button-small"
              onClick={() => setShowRejectForm(true)}
            >
              Edit Rejection Reason
            </button>
          </div>
          <p className="rejection-banner-text">{attraction.rejectionReason}</p>
        </div>
      )}

      <div className="review-card">
        {/* Basic Info */}
        <DetailSection label="Attraction Information">
          <div className="review-facts">
            <div>
              <span>Name</span>
              <strong>{attraction.name}</strong>
            </div>
            <div>
              <span>Category</span>
              <strong>{attraction.category?.name || 'Uncategorised'}</strong>
            </div>
            <div>
              <span>Submitted On</span>
              <strong>{formatDate(attraction.createdAt)}</strong>
            </div>
            <div>
              <span>Last Updated</span>
              <strong>{formatDate(attraction.updatedAt)}</strong>
            </div>
            <div className="review-fact-wide">
              <span>Description</span>
              <strong>{attraction.description}</strong>
            </div>
          </div>
        </DetailSection>

        {/* Location & Pricing */}
        <DetailSection label="Location, Pricing & Provider">
          <div className="review-facts">
            <div>
              <span>District</span>
              <strong>{attraction.district}</strong>
            </div>
            <div>
              <span>Address</span>
              <strong>{attraction.address}</strong>
            </div>
            <div>
              <span>Coordinates</span>
              <strong>{attraction.latitude}, {attraction.longitude}</strong>
            </div>
            <div>
              <span>Price</span>
              <strong>LKR {attraction.price}</strong>
            </div>
            <div className="review-fact-wide">
              <span>Provider ID</span>
              <code>{attraction.providerId}</code>
            </div>
          </div>
        </DetailSection>

        {/* Images */}
        {attraction.images?.length > 0 && (
          <DetailSection label={`Images (${attraction.images.length})`}>
            <div className="admin-image-gallery">
              {attraction.images.map((img) => (
                <div key={img.id} className="admin-gallery-item">
                  <img src={img.imageUrl} alt={img.altText || attraction.name} />
                  {img.isPrimary && <span className="primary-pill">Primary</span>}
                </div>
              ))}
            </div>
          </DetailSection>
        )}

        {/* Schedule and availability */}
        <DetailSection label="Opening Schedules & Experience Slots">
          <div className="review-operational-list">
            <h4>Opening Days</h4>
            {attraction.schedules?.length ? (
              attraction.schedules.map((schedule) => (
                <div key={schedule.id}>
                  <strong>{schedule.dayOfWeek}</strong>
                  <span>
                    {schedule.isClosed
                      ? 'Closed'
                      : `${timeValue(schedule.openingTime)} – ${timeValue(schedule.closingTime)}`}
                  </span>
                </div>
              ))
            ) : (
              <p className="muted">No regular opening schedules provided.</p>
            )}

            <h4 style={{ marginTop: '1rem' }}>Experience Slots</h4>
            {attraction.experienceSlots?.length ? (
              attraction.experienceSlots.map((slot) => (
                <div key={slot.id}>
                  <strong>{slot.date}</strong>
                  <span>
                    {timeValue(slot.startTime)} – {timeValue(slot.endTime)} · {slot.availableCapacity} of{' '}
                    {slot.capacity} spots available
                  </span>
                </div>
              ))
            ) : (
              <p className="muted">No specific experience slots provided.</p>
            )}
          </div>
        </DetailSection>

        {/* Decision & Action Area */}
        <div className="review-decision">
          <div>
            <p className="eyebrow">Administrative Action</p>
            <h2>Workflow Decision</h2>
            <p>
              Approve this attraction to publish it for tourists, or reject it with actionable feedback so the provider can address the issues.
            </p>
          </div>

          <div className="admin-decision-actions">
            {/* Status dropdown quick override */}
            <div className="status-dropdown-group">
              <label htmlFor="admin-detail-status-select">Status selector:</label>
              <select
                id="admin-detail-status-select"
                className="admin-status-dropdown"
                value={attraction.status}
                disabled={saving}
                onChange={(e) => handleStatusChange(e.target.value)}
              >
                <option value="PendingApproval">Pending Approval</option>
                <option value="Approved">Approved</option>
                <option value="Rejected">Rejected</option>
                <option value="UnderReview">Under Review</option>
              </select>
            </div>

            <div className="decision-buttons">
              {attraction.status !== 'Approved' && (
                <button
                  className="button button-primary"
                  type="button"
                  onClick={handleApprove}
                  disabled={saving}
                >
                  {saving ? 'Approving…' : 'Approve Attraction'}
                </button>
              )}

              <button
                className="button button-danger"
                type="button"
                onClick={() => setShowRejectForm((prev) => !prev)}
                disabled={saving}
              >
                {attraction.status === 'Rejected' ? 'Update Rejection Feedback' : 'Reject Attraction'}
              </button>

              {attraction.status !== 'PendingApproval' && (
                <button
                  className="button button-secondary-light"
                  type="button"
                  onClick={() => handleStatusChange('PendingApproval')}
                  disabled={saving}
                >
                  Review Again / Reset to Pending
                </button>
              )}
            </div>
          </div>
        </div>

        {/* Inline Rejection Reason Form */}
        {showRejectForm && (
          <div className="admin-inline-reject-box">
            <h3>Provide Rejection Feedback for Provider</h3>
            <p className="lead" style={{ fontSize: '0.95rem' }}>
              Explain clearly what changes or additions are required. The provider will see this message in their dashboard and will be able to edit and resubmit the attraction for another review.
            </p>
            <form onSubmit={handleReject}>
              <textarea
                className="form-textarea"
                rows="4"
                placeholder="e.g. Please clarify ticket pricing, upload higher-resolution primary photos, or fix address accuracy."
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
                required
              />
              <div style={{ display: 'flex', gap: '0.75rem', marginTop: '1rem', justifyContent: 'flex-end' }}>
                <button
                  type="button"
                  className="button button-secondary-light"
                  onClick={() => setShowRejectForm(false)}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="button button-danger"
                  disabled={saving || !rejectReason.trim()}
                >
                  {saving ? 'Saving…' : 'Submit Rejection & Notify Provider'}
                </button>
              </div>
            </form>
          </div>
        )}
      </div>
    </section>
  )
}
