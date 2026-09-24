import { useEffect, useState } from 'react'
import { getTravelAlert, getTravelAlerts } from '../api/travelAlerts'

const PAGE_SIZE = 12
const severities = ['Low', 'Medium', 'High', 'Critical']
const alertTypes = ['Weather', 'RoadClosure', 'Transport', 'Safety', 'SiteClosure', 'Event', 'General']
const initialFilters = {
  search: '',
  district: '',
  status: 'Active',
  severity: '',
  alertType: '',
  sortBy: 'startDateTime',
  sortDirection: 'asc',
}
const dateFormatter = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })

function formatLabel(value = '') {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function formatDate(value) {
  if (!value) return 'Date unavailable'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? 'Date unavailable' : dateFormatter.format(date)
}

function getErrorMessage(error, fallback = 'The request could not be completed.') {
  return error.response?.data?.message || fallback
}

function severityClass(value) {
  return 'status-badge severity-' + value.toLowerCase()
}

function statusClass(value) {
  return 'status-badge status-' + value.toLowerCase()
}

function DetailItem({ label, value }) {
  return (
    <div className="detail-item">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function AdvisoryDetail({ alert, onClose }) {
  return (
    <div className="alert-modal-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <section className="alert-modal alert-detail" role="dialog" aria-modal="true" aria-labelledby="advisory-detail-title">
        <div className="alert-modal-heading">
          <div>
            <p className="eyebrow">Travel advisory</p>
            <h2 id="advisory-detail-title">{alert.title}</h2>
          </div>
          <button className="modal-close" type="button" onClick={onClose} aria-label="Close advisory details">×</button>
        </div>
        <div className="advisory-detail-badges">
          <span className={severityClass(alert.severity)}>{alert.severity} priority</span>
          <span className={statusClass(alert.status)}>{formatLabel(alert.status)}</span>
        </div>
        <p className="alert-detail-description">{alert.description}</p>
        <div className="detail-grid">
          <DetailItem label="District" value={alert.district} />
          <DetailItem label="Alert type" value={formatLabel(alert.alertType)} />
          <DetailItem label="Starts" value={formatDate(alert.startDateTime)} />
          <DetailItem label="Ends" value={formatDate(alert.endDateTime)} />
          <DetailItem label="Source" value={alert.source || 'Not provided'} />
        </div>
        <div className="modal-actions">
          <button className="button button-secondary-light" type="button" onClick={onClose}>Close</button>
        </div>
      </section>
    </div>
  )
}

export default function TravelAdvisoriesPage() {
  const [filters, setFilters] = useState(initialFilters)
  const [page, setPage] = useState(1)
  const [result, setResult] = useState(null)
  const [selectedAlert, setSelectedAlert] = useState(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isDetailLoading, setIsDetailLoading] = useState(false)
  const [error, setError] = useState('')
  const [detailError, setDetailError] = useState('')

  useEffect(() => {
    let current = true
    getTravelAlerts({ ...filters, page, pageSize: PAGE_SIZE })
      .then((nextResult) => { if (current) setResult(nextResult) })
      .catch((requestError) => {
        if (current) {
          setResult(null)
          setError(getErrorMessage(requestError, 'Unable to load travel advisories. Please try again.'))
        }
      })
      .finally(() => { if (current) setIsLoading(false) })
    return () => { current = false }
  }, [filters, page])

  function updateFilter(event) {
    const { name, value } = event.target
    setIsLoading(true)
    setError('')
    setFilters((current) => ({ ...current, [name]: value }))
    setPage(1)
  }

  function clearFilters() {
    setIsLoading(true)
    setError('')
    setFilters(initialFilters)
    setPage(1)
  }

  function goToPage(nextPage) {
    setIsLoading(true)
    setPage(nextPage)
  }

  async function openDetail(alert) {
    setSelectedAlert(null)
    setDetailError('')
    setIsDetailLoading(true)
    try {
      setSelectedAlert(await getTravelAlert(alert.id))
    } catch (requestError) {
      setDetailError(getErrorMessage(requestError, 'Unable to load this advisory. Please try again.'))
    } finally {
      setIsDetailLoading(false)
    }
  }

  const alerts = result?.items || []
  const totalPages = result?.totalPages || 0

  return (
    <section className="travel-advisories-page" aria-labelledby="travel-advisories-title">
      <div className="page-header advisories-header">
        <div>
          <p className="eyebrow">Journey awareness</p>
          <h1 id="travel-advisories-title">Travel Advisories</h1>
          <p className="lead">Stay informed about weather, road, transport, safety, and site conditions that may affect your journey.</p>
        </div>
        <div className="advisories-current-note">
          <strong>Current view</strong>
          <span>{filters.status === 'Active' ? 'Active advisories first' : 'All available advisories'}</span>
        </div>
      </div>

      <div className="advisory-filter-panel">
        <div className="filter-heading">
          <div>
            <p className="eyebrow">Plan with awareness</p>
            <h2>Find an advisory</h2>
          </div>
          <button className="button button-secondary-light" type="button" onClick={clearFilters}>Clear filters</button>
        </div>
        <div className="advisory-filter-grid">
          <label className="filter-field advisory-search-field">Search
            <input name="search" type="search" placeholder="Search title or description" value={filters.search} onChange={updateFilter} />
          </label>
          <label className="filter-field">District
            <input name="district" placeholder="e.g. Kandy" value={filters.district} onChange={updateFilter} />
          </label>
          <label className="filter-field">Severity
            <select name="severity" value={filters.severity} onChange={updateFilter}>
              <option value="">All severities</option>
              {severities.map((value) => <option key={value} value={value}>{value}</option>)}
            </select>
          </label>
          <label className="filter-field">Alert type
            <select name="alertType" value={filters.alertType} onChange={updateFilter}>
              <option value="">All alert types</option>
              {alertTypes.map((value) => <option key={value} value={value}>{formatLabel(value)}</option>)}
            </select>
          </label>
          <label className="filter-field">Show
            <select name="status" value={filters.status} onChange={updateFilter}>
              <option value="Active">Current advisories</option>
              <option value="">All statuses</option>
            </select>
          </label>
        </div>
      </div>

      <div className="advisory-list-panel">
        <div className="list-heading">
          <div>
            <p className="eyebrow">Before you set out</p>
            <h2>Advisories affecting travel</h2>
            {result && <p className="muted">Showing page {result.page} of {result.totalPages || 1} · {result.totalCount} matching advisories</p>}
          </div>
        </div>
        {isLoading && <div className="state-message" role="status">Loading travel advisories…</div>}
        {!isLoading && error && <div className="state-message state-error" role="alert">{error}</div>}
        {!isLoading && !error && alerts.length === 0 && (
          <div className="state-message">
            <strong>No travel advisories found.</strong>
            <span>Try another search or view all statuses.</span>
          </div>
        )}
        {!isLoading && !error && alerts.length > 0 && (
          <div className="advisory-card-grid">
            {alerts.map((alert) => (
              <button className="advisory-card" type="button" key={alert.id} onClick={() => openDetail(alert)}>
                <span className="advisory-card-heading">
                  <strong>{alert.title}</strong>
                  <span className={severityClass(alert.severity)}>{alert.severity}</span>
                </span>
                <span className="advisory-card-meta">{alert.district} · {formatLabel(alert.alertType)}</span>
                <span className="advisory-card-description">{alert.description}</span>
                <span className="advisory-card-footer">
                  <span className={statusClass(alert.status)}>{formatLabel(alert.status)}</span>
                  <span>{formatDate(alert.startDateTime)}</span>
                </span>
              </button>
            ))}
          </div>
        )}
        <div className="pagination-bar">
          <span className="muted">{result?.totalCount ?? 0} matching advisories</span>
          <div className="pagination-controls">
            <button className="button button-secondary-light" type="button" disabled={isLoading || page <= 1} onClick={() => goToPage(page - 1)}>Previous</button>
            <span>Page {page} of {totalPages || 1}</span>
            <button className="button button-secondary-light" type="button" disabled={isLoading || page >= totalPages} onClick={() => goToPage(page + 1)}>Next</button>
          </div>
        </div>
      </div>

      {isDetailLoading && <div className="alert-modal-backdrop"><section className="alert-modal" role="dialog" aria-modal="true" aria-label="Loading advisory"><div className="state-message">Loading advisory details…</div></section></div>}
      {detailError && <p className="form-feedback advisory-detail-error" role="alert">{detailError}</p>}
      {selectedAlert && <AdvisoryDetail alert={selectedAlert} onClose={() => setSelectedAlert(null)} />}
    </section>
  )
}
