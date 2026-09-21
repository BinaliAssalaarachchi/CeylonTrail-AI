import { useEffect, useState } from 'react'
import { getTravelAlerts } from '../api/travelAlerts'

const PAGE_SIZE = 10

const initialFilters = {
  search: '',
  district: '',
  status: '',
  severity: '',
  alertType: '',
  sortBy: 'createdAt',
  sortDirection: 'desc',
}

const statuses = ['Draft', 'Active', 'Expired', 'Cancelled']
const severities = ['Low', 'Medium', 'High', 'Critical']
const alertTypes = ['Weather', 'RoadClosure', 'Transport', 'Safety', 'SiteClosure', 'Event', 'General']

const dateFormatter = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
})

function formatLabel(value) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function formatDate(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : dateFormatter.format(date)
}

function getErrorMessage(error) {
  return error.response?.data?.message || 'Unable to load travel alerts. Please try again.'
}

function SummaryCard({ label, value, detail }) {
  return (
    <article className="summary-card">
      <p>{label}</p>
      <strong>{value}</strong>
      <span>{detail}</span>
    </article>
  )
}

export default function TravelAlertsPage() {
  const [filters, setFilters] = useState(initialFilters)
  const [page, setPage] = useState(1)
  const [result, setResult] = useState(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let isCurrentRequest = true

    async function loadAlerts() {
      setIsLoading(true)
      setError('')
      try {
        const nextResult = await getTravelAlerts({ ...filters, page, pageSize: PAGE_SIZE })
        if (isCurrentRequest) setResult(nextResult)
      } catch (requestError) {
        if (isCurrentRequest) {
          setResult(null)
          setError(getErrorMessage(requestError))
        }
      } finally {
        if (isCurrentRequest) setIsLoading(false)
      }
    }

    loadAlerts()
    return () => {
      isCurrentRequest = false
    }
  }, [filters, page])

  function updateFilter(event) {
    const { name, value } = event.target
    setFilters((current) => ({ ...current, [name]: value }))
    setPage(1)
  }

  function clearFilters() {
    setFilters(initialFilters)
    setPage(1)
  }

  const alerts = result?.items || []
  const activeCount = alerts.filter((alert) => alert.status === 'Active').length
  const highCriticalCount = alerts.filter((alert) => ['High', 'Critical'].includes(alert.severity)).length
  const draftCount = alerts.filter((alert) => alert.status === 'Draft').length
  const totalPages = result?.totalPages || 0

  return (
    <section className="alerts-page" aria-labelledby="travel-alerts-title">
      <div className="page-header alerts-header">
        <div>
          <p className="eyebrow">Island-wide tactical routing &amp; safety matrix</p>
          <h1 id="travel-alerts-title">Travel Operations</h1>
          <p className="lead">Monitor and manage travel advisories affecting journeys across Sri Lanka.</p>
        </div>
        <button
          className="button button-primary alerts-create-button"
          type="button"
          disabled
          title="Alert creation will be available in a later phase"
        >
          <span aria-hidden="true">+</span> Create Travel Alert
        </button>
      </div>

      <div className="summary-grid" aria-label="Current page summary">
        <SummaryCard label="Matching alerts" value={result?.totalCount ?? '—'} detail="All filtered results" />
        <SummaryCard label="Active alerts" value={activeCount} detail="On this page" />
        <SummaryCard label="High / Critical" value={highCriticalCount} detail="On this page" />
        <SummaryCard label="Draft alerts" value={draftCount} detail="On this page" />
      </div>

      <div className="filter-panel">
        <div className="filter-heading">
          <div>
            <p className="eyebrow">Find an advisory</p>
            <h2>Search and filters</h2>
          </div>
          <button className="button button-secondary-light" type="button" onClick={clearFilters}>Clear filters</button>
        </div>
        <div className="filter-grid">
          <label className="filter-field filter-field-wide">
            Search
            <input name="search" type="search" placeholder="Search title, description or district" value={filters.search} onChange={updateFilter} />
          </label>
          <label className="filter-field">
            District
            <input name="district" type="text" placeholder="e.g. Kandy" value={filters.district} onChange={updateFilter} />
          </label>
          <label className="filter-field">
            Status
            <select name="status" value={filters.status} onChange={updateFilter}>
              <option value="">All statuses</option>
              {statuses.map((status) => <option key={status} value={status}>{status}</option>)}
            </select>
          </label>
          <label className="filter-field">
            Severity
            <select name="severity" value={filters.severity} onChange={updateFilter}>
              <option value="">All severities</option>
              {severities.map((severity) => <option key={severity} value={severity}>{severity}</option>)}
            </select>
          </label>
          <label className="filter-field">
            Alert type
            <select name="alertType" value={filters.alertType} onChange={updateFilter}>
              <option value="">All alert types</option>
              {alertTypes.map((alertType) => <option key={alertType} value={alertType}>{formatLabel(alertType)}</option>)}
            </select>
          </label>
          <label className="filter-field">
            Sort by
            <select name="sortBy" value={filters.sortBy} onChange={updateFilter}>
              <option value="createdAt">Created date</option>
              <option value="updatedAt">Updated date</option>
              <option value="startDateTime">Start date</option>
              <option value="endDateTime">End date</option>
              <option value="severity">Severity</option>
              <option value="district">District</option>
              <option value="title">Title</option>
            </select>
          </label>
          <label className="filter-field">
            Direction
            <select name="sortDirection" value={filters.sortDirection} onChange={updateFilter}>
              <option value="desc">Newest first</option>
              <option value="asc">Oldest first</option>
            </select>
          </label>
        </div>
      </div>

      <div className="alerts-list-panel">
        <div className="list-heading">
          <div>
            <p className="eyebrow">Operational dispatches</p>
            <h2>Advisories</h2>
            {result && <p className="muted">Showing page {result.page} of {result.totalPages || 1} · {result.totalCount} matching alerts</p>}
          </div>
        </div>

        {isLoading && <div className="state-message" role="status">Loading travel alerts…</div>}
        {!isLoading && error && <div className="state-message state-error" role="alert">{error}</div>}
        {!isLoading && !error && alerts.length === 0 && (
          <div className="state-message">
            <strong>No travel alerts found.</strong>
            <span>Try changing your search or clearing the filters.</span>
          </div>
        )}
        {!isLoading && !error && alerts.length > 0 && (
          <div className="table-wrapper">
            <table className="alerts-table">
              <thead>
                <tr>
                  <th scope="col">Alert</th>
                  <th scope="col">District</th>
                  <th scope="col">Severity</th>
                  <th scope="col">Status</th>
                  <th scope="col">Start</th>
                  <th scope="col">End</th>
                  <th scope="col">Source</th>
                  <th scope="col"><span className="sr-only">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                {alerts.map((alert) => (
                  <tr key={alert.id}>
                    <td>
                      <strong>{alert.title}</strong>
                      <span className="table-subtext">{formatLabel(alert.alertType)}</span>
                    </td>
                    <td><span className="table-district">{alert.district}</span></td>
                    <td><span className={`status-badge severity-${alert.severity.toLowerCase()}`}>{alert.severity}</span></td>
                    <td><span className={`status-badge status-${alert.status.toLowerCase()}`}>{alert.status}</span></td>
                    <td><time dateTime={alert.startDateTime}>{formatDate(alert.startDateTime)}</time></td>
                    <td><time dateTime={alert.endDateTime}>{formatDate(alert.endDateTime)}</time></td>
                    <td>{alert.source || '—'}</td>
                    <td>
                      <div className="table-actions">
                        <button className="text-action" type="button" disabled title="Available in a later phase">View</button>
                        <button className="text-action" type="button" disabled title="Available in a later phase">Edit</button>
                        <button className="text-action text-action-danger" type="button" disabled title="Available in a later phase">Delete</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="pagination-bar">
          <span className="muted">{result?.totalCount ?? 0} matching alerts</span>
          <div className="pagination-controls">
            <button className="button button-secondary-light" type="button" disabled={isLoading || page <= 1} onClick={() => setPage((current) => current - 1)}>Previous</button>
            <span>Page {page} of {totalPages || 1}</span>
            <button className="button button-secondary-light" type="button" disabled={isLoading || page >= totalPages} onClick={() => setPage((current) => current + 1)}>Next</button>
          </div>
        </div>
      </div>
    </section>
  )
}
