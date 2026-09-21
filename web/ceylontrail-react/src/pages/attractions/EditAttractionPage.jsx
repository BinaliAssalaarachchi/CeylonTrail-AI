import { useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { getAttractionById, getCategories, updateAttraction } from '../../api/attractions'
import AttractionForm from '../../components/attractions/AttractionForm'
import StatusBadge from '../../components/attractions/StatusBadge'
import { apiErrorMessage } from './attractionUtils'

export default function EditAttractionPage() {
  const { id } = useParams(); const location = useLocation(); const [attraction, setAttraction] = useState(null); const [categories, setCategories] = useState([]); const [loading, setLoading] = useState(true); const [saving, setSaving] = useState(false); const [error, setError] = useState(''); const [message, setMessage] = useState(location.state?.message || '')
  useEffect(() => { Promise.all([getAttractionById(id), getCategories()]).then(([item, categoryList]) => { setAttraction(item); setCategories(categoryList) }).catch((e) => setError(apiErrorMessage(e, 'Unable to load this attraction.'))).finally(() => setLoading(false)) }, [id])
  async function submit(data) { setSaving(true); setError(''); try { const updated = await updateAttraction(id, data); setAttraction(updated); setMessage('Attraction updated successfully.') } catch (e) { setError(apiErrorMessage(e, 'Unable to update attraction.')) } finally { setSaving(false) } }
  if (loading) return <section className="page-section"><p className="notice">Loading attraction…</p></section>
  if (error && !attraction) return <section className="page-section"><p className="form-error notice-error">{error}</p><Link className="button button-secondary dark-button" to="/provider/attractions">Back to attractions</Link></section>
  return <section className="page-section wide-page"><Link className="back-link" to="/provider/attractions">← My attractions</Link><div className="page-header"><div><p className="eyebrow">Manage attraction</p><h1>Edit {attraction.name}</h1></div><StatusBadge status={attraction.status} isActive={attraction.isActive} /></div>{message && <p className="success-message" role="status">{message}</p>}<AttractionForm key={attraction.id} categories={categories} initialValues={attraction} onSubmit={submit} isSubmitting={saving} serverError={error} /><div className="sub-navigation"><Link to={`/provider/attractions/${id}/schedules`}>Manage schedules</Link><Link to={`/provider/attractions/${id}/slots`}>Manage experience slots</Link><Link to={`/provider/attractions/${id}/availability`}>View availability</Link></div></section>
}
