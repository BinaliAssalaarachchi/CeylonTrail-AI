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
        if (isMounted) setError(err.response?.data?.message || 'Unable to load attractions right now.')
      } finally {
        if (isMounted) setLoading(false)
      }
    }
    init()
    return () => { isMounted = false }
  }, [])

  const filteredAttractions = attractions.filter((item) => {
    const term = search.toLowerCase()
    const matchesSearch = !term || item.name?.toLowerCase().includes(term) || item.description?.toLowerCase().includes(term)
    const matchesCategory = !selectedCategory || String(item.categoryId) === String(selectedCategory)
    const matchesDistrict = !district || item.district?.toLowerCase().includes(district.toLowerCase())
    return matchesSearch && matchesCategory && matchesDistrict
  })

  const imageFor = (attraction) => {
    const candidate = attraction.primaryImageUrl || attraction.imageUrl || attraction.images?.[0]?.imageUrl
    return candidate || (attraction.name?.toLowerCase().includes('sigiriya') ? '/images/sigiriya-hero.png' : '/images/tea-country-hero.jpg')
  }

  return (
    <section className="workspace-page" aria-labelledby="discover-title">
      <div className="page-heading discover-heading"><div><p className="eyebrow">The island, thoughtfully</p><h1 id="discover-title">Discover Sri Lanka</h1><p className="lead">Heritage sites, quiet coastlines, and experiences worth taking the long way to.</p></div><div className="discover-actions">{isProvider && <Link className="button button-primary" to="/provider/attractions">Manage my listings</Link>}{isAdmin && <Link className="button button-primary" to="/admin/attractions">Review pending listings</Link>}</div></div>
      <div className="filter-panel discover-filters"><label className="filter-field filter-field-wide">Search<input type="search" placeholder="Search places and experiences" value={search} onChange={(e) => setSearch(e.target.value)} /></label><label className="filter-field">Category<select value={selectedCategory} onChange={(e) => setSelectedCategory(e.target.value)}><option value="">All categories</option>{categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label><label className="filter-field">District<input placeholder="Kandy, Galle..." value={district} onChange={(e) => setDistrict(e.target.value)} /></label></div>
      {loading && <div className="state-card">Loading island experiences...</div>}
      {error && <div className="state-card state-error" role="alert">{error}</div>}
      {!loading && !error && filteredAttractions.length === 0 && <div className="state-card"><strong>No experiences found.</strong><span>Try a different search or browse all categories.</span></div>}
      {!loading && !error && filteredAttractions.length > 0 && <div className="experience-grid">{filteredAttractions.map((attr) => <article key={attr.id} className="experience-card"><div className="experience-image-wrap"><img src={imageFor(attr)} alt="" className="experience-image" loading="lazy" onError={(event) => { event.currentTarget.src = '/images/tea-country-hero.jpg' }} /><span className="experience-location">{attr.district || 'Sri Lanka'}</span><span className="experience-image-mark" aria-hidden="true">✦</span></div><div className="experience-body"><span className="experience-category">{attr.category?.name || attr.categoryName || 'Experience'}</span><h3>{attr.name}</h3><p>{attr.description?.length > 120 ? `${attr.description.slice(0, 120)}...` : (attr.description || 'A place to linger, explore, and remember.')}</p></div><div className="experience-footer"><strong>{attr.price ? `LKR ${Number(attr.price).toLocaleString('en-LK')}` : 'Free entry'}</strong><Link to="/bookings" className="experience-link">Open details <span aria-hidden="true">→</span></Link></div></article>)}</div>}
    </section>
  )
}
