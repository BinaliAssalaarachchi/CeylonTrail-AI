import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { approveAttraction, getAdminAttractions, rejectAttraction, setAttractionStatus } from '../../api/attractions'
import StatusBadge from '../../components/attractions/StatusBadge'
import { apiErrorMessage } from '../attractions/attractionUtils'
import { primaryAttractionImageFor } from '../../utils/attractionImages'

function formatDate(value) {
  if (!value) return 'N/A'
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric' }).format(new Date(value))
}

export default function PendingAttractionsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const initialStatus = searchParams.get('status') || ''

  const [result, setResult] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [actionId, setActionId] = useState('')
  const [message, setMessage] = useState('')

  const [statusFilter, setStatusFilter] = useState(initialStatus)
  const [searchQuery, setSearchQuery] = useState('')
  const [districtFilter, setDistrictFilter] = useState('')

  // Reject Modal State
  const [rejectModalAttraction, setRejectModalAttraction] = useState(null)
  const [rejectReason, setRejectReason] = useState('')
  const [rejectSubmitting, setRejectSubmitting] = useState(false)
  const [rejectError, setRejectError] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const data = await getAdminAttractions({
        page: 1,
        pageSize: 100,
        status: statusFilter && statusFilter !== 'all' ? statusFilter : undefined,
      })
      setResult(data)
    } catch (e) {
      setError(apiErrorMessage(e, 'Unable to load attractions.'))
    } finally {
      setLoading(false)
    }
  }, [statusFilter])

  useEffect(() => {
    load()
  }, [load])

  const handleStatusTabChange = (newStatus) => {
    setStatusFilter(newStatus)
    if (newStatus && newStatus !== 'all') {
      setSearchParams({ status: newStatus })
    } else {
      setSearchParams({})
    }
  }

  async function handleApprove(attraction) {
    setActionId(attraction.id)
    setError('')
    try {
      await approveAttraction(attraction.id)
      setMessage(`“${attraction.name}” was approved successfully.`)
      await load()
    } catch (e) {
      setError(apiErrorMessage(e, 'Unable to approve this attraction.'))
    } finally {
      setActionId('')
    }
  }

  function openRejectModal(attraction) {
    setRejectModalAttraction(attraction)
    setRejectReason(attraction.rejectionReason || '')
    setRejectError('')
  }

  function closeRejectModal() {
    setRejectModalAttraction(null)
    setRejectReason('')
    setRejectError('')
  }

  async function submitReject(e) {
    e?.preventDefault()
    if (!rejectReason.trim()) {
      setRejectError('Please provide a reason for rejecting this attraction.')
      return
    }

    setRejectSubmitting(true)
    setRejectError('')
    try {
      await rejectAttraction(rejectModalAttraction.id, rejectReason.trim())
      setMessage(`“${rejectModalAttraction.name}” was marked as Rejected and feedback was saved.`)
      closeRejectModal()
      await load()
    } catch (e) {
      setRejectError(apiErrorMessage(e, 'Unable to reject this attraction.'))
    } finally {
      setRejectSubmitting(false)
    }
  }

  async function handleStatusChange(attraction, newStatus) {
    if (newStatus === 'Rejected') {
      openRejectModal(attraction)
      return
    }

    setActionId(attraction.id)
    setError('')
    try {
      await setAttractionStatus(attraction.id, newStatus)
      setMessage(`“${attraction.name}” status updated to ${newStatus}.`)
      await load()
    } catch (e) {
      setError(apiErrorMessage(e, 'Unable to update status.'))
    } finally {
      setActionId('')
    }
  }

  const rawAttractions = result?.items || []

  const districts = useMemo(() => {
    return [...new Set(rawAttractions.map((item) => item.district).filter(Boolean))].sort()
  }, [rawAttractions])

  const filteredAttractions = useMemo(() => {
    const query = searchQuery.trim().toLowerCase()
    return rawAttractions.filter((attraction) => {
      const matchSearch =
        !query ||
        attraction.name.toLowerCase().includes(query) ||
        attraction.description?.toLowerCase().includes(query) ||
        attraction.district?.toLowerCase().includes(query) ||
        attraction.category?.name?.toLowerCase().includes(query)

      const matchDistrict = !districtFilter || attraction.district === districtFilter
      return matchSearch && matchDistrict
    })
  }, [rawAttractions, searchQuery, districtFilter])

  const countByStatus = useMemo(() => {
    return {
      all: rawAttractions.length,
      pending: rawAttractions.filter((a) => a.status === 'PendingApproval').length,
      approved: rawAttractions.filter((a) => a.status === 'Approved').length,
      rejected: rawAttractions.filter((a) => a.status === 'Rejected').length,
      underReview: rawAttractions.filter((a) => a.status === 'UnderReview').length,
    }
  }, [rawAttractions])

  return (
    <section className="page-section wide-page admin-attractions-page">
      <div className="page-header consistent-page-header">
        <div>
          <p className="eyebrow">Attraction review</p>
          <h1>Attractions Management & Review</h1>
          <p className="lead">
            Review, approve, reject, or request changes on all provider attractions in the system.
          </p>
        </div>
        <Link className="button button-secondary-light" to="/administrator">
          Back to dashboard
        </Link>
      </div>

      {message && <p className="success-message" role="status">{message}</p>}
      {error && <p className="form-error notice-error" role="alert">{error}</p>}

      {/* Filter Tabs */}
      <div className="admin-status-tabs" role="tablist" aria-label="Filter by status">
        <button
          type="button"
          role="tab"
          aria-selected={statusFilter === '' || statusFilter === 'all'}
          className={`admin-status-tab ${!statusFilter || statusFilter === 'all' ? 'active' : ''}`}
          onClick={() => handleStatusTabChange('all')}
        >
          All Attractions
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={statusFilter === 'PendingApproval'}
          className={`admin-status-tab ${statusFilter === 'PendingApproval' ? 'active' : ''}`}
          onClick={() => handleStatusTabChange('PendingApproval')}
        >
          Pending Approval
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={statusFilter === 'Approved'}
          className={`admin-status-tab ${statusFilter === 'Approved' ? 'active' : ''}`}
          onClick={() => handleStatusTabChange('Approved')}
        >
          Approved
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={statusFilter === 'Rejected'}
          className={`admin-status-tab ${statusFilter === 'Rejected' ? 'active' : ''}`}
          onClick={() => handleStatusTabChange('Rejected')}
        >
          Rejected
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={statusFilter === 'UnderReview'}
          className={`admin-status-tab ${statusFilter === 'UnderReview' ? 'active' : ''}`}
          onClick={() => handleStatusTabChange('UnderReview')}
        >
          Under Review
        </button>
      </div>

      {/* Toolbar filters */}
      <div className="provider-attraction-toolbar admin-toolbar-row" aria-label="Filter attractions">
        <div className="provider-filter-grid" style={{ width: '100%' }}>
          <label className="provider-filter-search">
            Search
            <input
              type="search"
              placeholder="Search by name, district, keyword…"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </label>
          <label>
            District
            <select
              value={districtFilter}
              onChange={(e) => setDistrictFilter(e.target.value)}
            >
              <option value="">All districts</option>
              {districts.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
            </select>
          </label>
          {(searchQuery || districtFilter) && (
            <button
              className="button button-secondary-light"
              type="button"
              style={{ alignSelf: 'flex-end', height: '42px' }}
              onClick={() => {
                setSearchQuery('')
                setDistrictFilter('')
              }}
            >
              Clear filters
            </button>
          )}
        </div>
      </div>

      {loading && (
        <div className="state-card" role="status">
          <strong>Loading attractions…</strong>
          <span>Preparing the administrator review list.</span>
        </div>
      )}

      {!loading && !error && filteredAttractions.length === 0 && (
        <div className="empty-state">
          <h2>No attractions found.</h2>
          <p>
            {statusFilter
              ? `There are currently no attractions with status “${statusFilter}”.`
              : 'No attractions match your current search criteria.'}
          </p>
        </div>
      )}

      {!loading && !error && filteredAttractions.length > 0 && (
        <div className="admin-attraction-list">
          {filteredAttractions.map((attraction) => {
            const image =
              attraction.images?.find((item) => item?.isPrimary && item?.imageUrl) ||
              attraction.images?.find((item) => item?.imageUrl)
            const isProcessing = actionId === attraction.id

            return (
              <article className="admin-attraction-row" key={attraction.id}>
                <div className="admin-attraction-thumb">
                  {image || primaryAttractionImageFor(attraction.name) ? (
                    <img src={image?.imageUrl || primaryAttractionImageFor(attraction.name)} alt={image?.altText || attraction.name} />
                  ) : (
                    <span aria-hidden="true">✦</span>
                  )}
                </div>

                <div className="admin-attraction-copy">
                  <div className="admin-attraction-title">
                    <div>
                      <p className="eyebrow">{attraction.category?.name || 'Uncategorised'}</p>
                      <h2>{attraction.name}</h2>
                    </div>
                    <StatusBadge status={attraction.status} isActive={attraction.isActive} />
                  </div>

                  <p className="admin-attraction-meta">
                    <strong>District:</strong> {attraction.district} · <strong>Price:</strong> LKR {attraction.price} ·{' '}
                    <strong>Submitted:</strong> {formatDate(attraction.createdAt)} ·{' '}
                    <strong>Last Update:</strong> {formatDate(attraction.updatedAt)}
                  </p>
                  <p className="admin-attraction-provider">
                    <strong>Provider ID:</strong> <code>{attraction.providerId}</code>
                  </p>

                  {/* Rejection Note if present */}
                  {attraction.status === 'Rejected' && attraction.rejectionReason && (
                    <div className="admin-rejection-note" role="note">
                      <strong>⚠️ Rejection reason sent to provider:</strong>
                      <p>{attraction.rejectionReason}</p>
                    </div>
                  )}
                </div>

                <div className="admin-attraction-actions">
                  <Link className="button button-secondary-light" to={`/admin/attractions/${attraction.id}`}>
                    Review Details
                  </Link>

                  {/* Quick status dropdown changer */}
                  <div className="admin-status-select-wrap">
                    <select
                      className="admin-status-dropdown"
                      value={attraction.status}
                      disabled={isProcessing}
                      onChange={(e) => handleStatusChange(attraction, e.target.value)}
                      aria-label="Change attraction status"
                    >
                      <option value="PendingApproval">Pending Approval</option>
                      <option value="Approved">Approved</option>
                      <option value="Rejected">Rejected</option>
                      <option value="UnderReview">Under Review</option>
                    </select>
                  </div>

                  {attraction.status !== 'Approved' && (
                    <button
                      className="button button-primary"
                      type="button"
                      disabled={isProcessing}
                      onClick={() => handleApprove(attraction)}
                    >
                      {isProcessing ? 'Saving…' : 'Approve'}
                    </button>
                  )}

                  {attraction.status !== 'Rejected' && (
                    <button
                      className="button button-danger"
                      type="button"
                      disabled={isProcessing}
                      onClick={() => openRejectModal(attraction)}
                    >
                      Reject
                    </button>
                  )}
                </div>
              </article>
            )
          })}
        </div>
      )}

      {/* Reject Modal */}
      {rejectModalAttraction && (
        <div className="modal-overlay" role="dialog" aria-modal="true" aria-labelledby="reject-modal-title">
          <div className="modal-content admin-reject-modal">
            <h2 id="reject-modal-title">Reject Attraction: {rejectModalAttraction.name}</h2>
            <p className="lead">
              Specify the reason why this attraction is being rejected. The tourism provider will be notified and will see this feedback in their workspace so they can make corrections.
            </p>

            {rejectError && <p className="form-error notice-error" role="alert">{rejectError}</p>}

            <form onSubmit={submitReject}>
              <label htmlFor="reject-reason-input" className="form-label">
                <strong>Rejection Reason & Feedback for Provider *</strong>
              </label>
              <textarea
                id="reject-reason-input"
                rows="4"
                className="form-textarea"
                placeholder="e.g., Incomplete address details, price does not align with description, or unclear schedule. Please update and resubmit."
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
                required
              />

              <div className="modal-actions" style={{ marginTop: '1.25rem' }}>
                <button
                  type="button"
                  className="button button-secondary-light"
                  onClick={closeRejectModal}
                  disabled={rejectSubmitting}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="button button-danger"
                  disabled={rejectSubmitting || !rejectReason.trim()}
                >
                  {rejectSubmitting ? 'Rejecting…' : 'Confirm & Reject Attraction'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </section>
  )
}
