import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { deleteAttraction, getMyAttractions } from '../../api/attractions'
import AttractionCard from '../../components/attractions/AttractionCard'
import { apiErrorMessage } from './attractionUtils'
import ConfirmationModal from '../../components/ConfirmationModal'

const initialFilters = { search: '', status: '', category: '', district: '' }
const emptyAttractions = []

function managementStatus(attraction) {
  return attraction.isActive ? attraction.status || 'Unknown' : 'Inactive'
}

function labelForStatus(status) {
  return status === 'PendingApproval' ? 'Pending Approval' : status
}

export default function MyAttractionsPage() {
  const [result, setResult] = useState(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const location = useLocation()
  const [message, setMessage] = useState(location.state?.message || '')
  const [filters, setFilters] = useState(initialFilters)
  const [confirming, setConfirming] = useState(null)
  const [deactivating, setDeactivating] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setResult(await getMyAttractions({ page: 1, pageSize: 100 }))
    } catch (requestError) {
      setError(apiErrorMessage(requestError, 'Unable to load your attractions.'))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  async function deactivate(attraction) {
    setDeactivating(true)
    try {
      await deleteAttraction(attraction.id)
      setConfirming(null)
      setMessage('Attraction deactivated successfully.')
      load()
    } catch (requestError) {
      setError(apiErrorMessage(requestError))
      setConfirming(null)
    } finally { setDeactivating(false) }
  }

  const attractions = result?.items ?? emptyAttractions
  const categories = useMemo(() => [...new Set(attractions.map((item) => item.category?.name).filter(Boolean))].sort(), [attractions])
  const districts = useMemo(() => [...new Set(attractions.map((item) => item.district).filter(Boolean))].sort(), [attractions])
  const statuses = useMemo(() => [...new Set(attractions.map(managementStatus))].sort(), [attractions])
  const stats = useMemo(() => ({
    total: attractions.length,
    approved: attractions.filter((item) => item.isActive && item.status === 'Approved').length,
    pending: attractions.filter((item) => item.isActive && item.status === 'PendingApproval').length,
    inactive: attractions.filter((item) => !item.isActive).length,
  }), [attractions])
  const filteredAttractions = useMemo(() => {
    const search = filters.search.trim().toLowerCase()
    return attractions.filter((attraction) => {
      const searchable = [attraction.name, attraction.district, attraction.category?.name].filter(Boolean).join(' ').toLowerCase()
      return (!search || searchable.includes(search)) &&
        (!filters.status || managementStatus(attraction) === filters.status) &&
        (!filters.category || attraction.category?.name === filters.category) &&
        (!filters.district || attraction.district === filters.district)
    })
  }, [attractions, filters])
  const hasFilters = Object.values(filters).some(Boolean)

  function updateFilter(event) {
    setFilters((current) => ({ ...current, [event.target.name]: event.target.value }))
  }

  function clearFilters() {
    setFilters(initialFilters)
  }

  return (
    <section className="page-section wide-page provider-attractions-page">
      <div className="page-header provider-attractions-header">
        <div>
          <p className="eyebrow">Provider workspace</p>
          <h1>My Attractions</h1>
          <p className="lead">Manage the attractions and experiences you provide.</p>
        </div>
        <Link className="button button-primary" to="/provider/attractions/create">Create attraction</Link>
      </div>

      {message && <p className="success-message" role="status">{message}</p>}
      {loading && <p className="notice">Loading attractions…</p>}
      {error && <p className="form-error notice-error" role="alert">{error}</p>}

      {!loading && !error && attractions.length > 0 && <>
        <div className="provider-attraction-stats" aria-label="Attraction summary">
          <div className="provider-stat"><span>Total attractions</span><strong>{stats.total}</strong><small>Across your workspace</small></div>
          <div className="provider-stat"><span>Approved</span><strong>{stats.approved}</strong><small>Publicly available</small></div>
          <div className="provider-stat"><span>Pending approval</span><strong>{stats.pending}</strong><small>Awaiting review</small></div>
          <div className="provider-stat"><span>Inactive</span><strong>{stats.inactive}</strong><small>Soft deactivated</small></div>
        </div>

        <div className="provider-attraction-toolbar" aria-label="Filter attractions">
          <div className="provider-toolbar-heading">
            <div><p className="eyebrow">Your catalogue</p><h2>Find an attraction</h2></div>
            <span className="provider-result-count">{filteredAttractions.length} of {attractions.length} shown</span>
          </div>
          <div className="provider-filter-grid">
            <label className="provider-filter-search">Search attractions
              <input name="search" type="search" placeholder="Search your attractions…" value={filters.search} onChange={updateFilter} aria-label="Search your attractions" />
            </label>
            <label>Status
              <select name="status" value={filters.status} onChange={updateFilter} aria-label="Filter by status">
                <option value="">All statuses</option>
                {statuses.map((status) => <option key={status} value={status}>{labelForStatus(status)}</option>)}
              </select>
            </label>
            <label>Category
              <select name="category" value={filters.category} onChange={updateFilter} aria-label="Filter by category">
                <option value="">All categories</option>
                {categories.map((category) => <option key={category} value={category}>{category}</option>)}
              </select>
            </label>
            <label>District
              <select name="district" value={filters.district} onChange={updateFilter} aria-label="Filter by district">
                <option value="">All districts</option>
                {districts.map((district) => <option key={district} value={district}>{district}</option>)}
              </select>
            </label>
            {hasFilters && <button className="button button-secondary-light provider-clear-filters" type="button" onClick={clearFilters}>Clear filters</button>}
          </div>
        </div>

        {filteredAttractions.length > 0 && <div className="provider-attraction-grid">
          {filteredAttractions.map((attraction) => <AttractionCard key={attraction.id} attraction={attraction} onDeactivate={setConfirming} />)}
        </div>}
        {filteredAttractions.length === 0 && <div className="empty-state provider-filter-empty"><h2>No attractions match your filters.</h2><p>Try changing your search or clearing the active filters.</p><button className="button button-secondary-light" type="button" onClick={clearFilters}>Clear filters</button></div>}
      </>}

      {!loading && !error && attractions.length === 0 && <div className="empty-state"><h2>No attractions yet</h2><p>Create your first attraction to begin managing schedules and experience slots.</p><Link className="button button-primary" to="/provider/attractions/create">Create your first attraction</Link></div>}
      {confirming && <ConfirmationModal title="Deactivate attraction?" message="This attraction will no longer be visible to tourists. Its existing database record will be preserved." confirmLabel="Deactivate attraction" isLoading={deactivating} onConfirm={() => deactivate(confirming)} onClose={() => setConfirming(null)} />}
    </section>
  )
}
