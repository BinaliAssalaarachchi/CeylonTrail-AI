import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import apiClient from '../api/client'
import { getCategories } from '../api/attractions'
import { useAuth } from '../context/useAuth'

export default function DiscoverPage() {
  const { user } = useAuth()
  const isProvider = user?.role === 'TourismProvider'
  const isAdmin = user?.role === 'Administrator'

  const [attractions, setAttractions] = useState([])
  const [categories, setCategories] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const [search, setSearch] = useState('')
  const [selectedCategory, setSelectedCategory] = useState('')
  const [district, setDistrict] = useState('')

  useEffect(() => {
    let isMounted = true
    async function init() {
      try {
        setLoading(true)
        const [catsRes, attrRes] = await Promise.all([
          getCategories().catch(() => []),
          apiClient.get('/api/attractions').catch(() => ({ data: { items: [] } })),
        ])

        if (isMounted) {
          setCategories(Array.isArray(catsRes) ? catsRes : [])
          const items = Array.isArray(attrRes.data) ? attrRes.data : attrRes.data?.items || []
          setAttractions(items)
        }
      } catch (err) {
        if (isMounted) setError(err.response?.data?.message || 'Unable to load attractions.')
      } finally {
        if (isMounted) setLoading(false)
      }
    }
    init()
    return () => { isMounted = false }
  }, [])

  const filteredAttractions = attractions.filter((item) => {
    const matchesSearch = !search || item.name?.toLowerCase().includes(search.toLowerCase()) || item.description?.toLowerCase().includes(search.toLowerCase())
    const matchesCategory = !selectedCategory || String(item.categoryId) === String(selectedCategory)
    const matchesDistrict = !district || item.district?.toLowerCase().includes(district.toLowerCase())
    return matchesSearch && matchesCategory && matchesDistrict
  })

  return (
    <section className="workspace-page" aria-labelledby="discover-title">
      <div className="page-heading">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '1rem' }}>
          <div>
            <p className="eyebrow">Island Experience Register</p>
            <h1 id="discover-title">Discover Sri Lanka</h1>
            <p className="lead">Explore heritage sites, eco-trails, cultural places, and curated attractions across the island.</p>
          </div>
          <div style={{ display: 'flex', gap: '0.75rem' }}>
            {isProvider && (
              <Link className="button button-primary" to="/provider/attractions">
                + Manage My Listings
              </Link>
            )}
            {isAdmin && (
              <Link className="button button-primary" to="/admin/attractions">
                Review Pending Listings
              </Link>
            )}
          </div>
        </div>
      </div>

      {/* Filter Bar */}
      <div className="filter-panel" style={{ marginBottom: '1.5rem' }}>
        <div className="filter-grid">
          <label className="filter-field filter-field-wide">
            Search
            <input
              type="search"
              placeholder="Search attractions, experiences, keywords..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <label className="filter-field">
            Category
            <select value={selectedCategory} onChange={(e) => setSelectedCategory(e.target.value)}>
              <option value="">All Categories</option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
          </label>
          <label className="filter-field">
            District
            <input
              placeholder="e.g. Kandy, Galle"
              value={district}
              onChange={(e) => setDistrict(e.target.value)}
            />
          </label>
        </div>
      </div>

      {loading && <div className="state-card">Loading attractions and experiences…</div>}
      {error && <div className="state-card state-error" role="alert">{error}</div>}

      {!loading && !error && filteredAttractions.length === 0 && (
        <div className="state-card">
          <strong>No attractions found.</strong>
          <span>Try adjusting your search criteria or check back once providers publish new experiences.</span>
        </div>
      )}

      {/* Attractions Grid */}
      {!loading && !error && filteredAttractions.length > 0 && (
        <div className="module-grid" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: '1.25rem' }}>
          {filteredAttractions.map((attr) => (
            <article key={attr.id} className="module-card" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
              <div>
                <span className="module-icon" aria-hidden="true">◉</span>
                <div style={{ marginTop: '0.5rem' }}>
                  <span className="status-badge" style={{ fontSize: '0.75rem', marginBottom: '0.5rem', display: 'inline-block' }}>
                    {attr.district || 'Sri Lanka'}
                  </span>
                  <h3 style={{ margin: '0.25rem 0' }}>{attr.name}</h3>
                  <p style={{ fontSize: '0.9rem', color: 'var(--color-muted)', lineHeight: '1.4' }}>
                    {attr.description?.length > 120 ? `${attr.description.slice(0, 120)}...` : attr.description}
                  </p>
                </div>
              </div>
              <div style={{ marginTop: '1rem', paddingTop: '0.75rem', borderTop: '1px solid var(--color-border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <strong style={{ color: 'var(--color-primary, #005a36)' }}>
                  {attr.price ? `LKR ${Number(attr.price).toLocaleString('en-LK')}` : 'Free Entry'}
                </strong>
                <Link to="/bookings" className="button button-secondary-light" style={{ padding: '0.35rem 0.75rem', fontSize: '0.85rem' }}>
                  View booking operations →
                </Link>
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  )
}
