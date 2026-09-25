import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { getAttractionById, getCategories, updateAttraction } from '../../api/attractions'
import AttractionForm from '../../components/attractions/AttractionForm'
import StatusBadge from '../../components/attractions/StatusBadge'
import { apiErrorMessage } from './attractionUtils'

export default function EditAttractionPage() {
  const { id } = useParams(); const location = useLocation(); const navigate = useNavigate(); const [attraction, setAttraction] = useState(null); const [categories, setCategories] = useState([]); const [loading, setLoading] = useState(true); const [saving, setSaving] = useState(false); const [error, setError] = useState(''); const [message, setMessage] = useState(location.state?.message || '')
  useEffect(() => { Promise.all([getAttractionById(id), getCategories()]).then(([item, categoryList]) => { setAttraction(item); setCategories(categoryList) }).catch((e) => setError(apiErrorMessage(e, 'Unable to load this attraction.'))).finally(() => setLoading(false)) }, [id])
  async function submit(data) { setSaving(true); setError(''); try { const updated = await updateAttraction(id, data); setAttraction(updated); setMessage('Attraction updated successfully.') } catch (e) { setError(apiErrorMessage(e, 'Unable to update attraction.')) } finally { setSaving(false) } }
  if (loading) return <section className="page-section"><div className="state-card" role="status"><strong>Loading attraction…</strong><span>Preparing the management form.</span></div></section>
  if (error && !attraction) return <section className="page-section"><div className="state-card state-error" role="alert"><strong>{error}</strong><span>We could not load this attraction.</span></div><Link className="button button-secondary-light" to="/provider/attractions">Back to attractions</Link></section>
  return <section className="page-section form-page"><Link className="back-link" to="/provider/attractions">← My attractions</Link><div className="consistent-page-header page-header"><div><p className="eyebrow">Provider workspace</p><h1>Edit attraction</h1><p className="lead">Update the information visitors and administrators use for this attraction.</p></div><StatusBadge status={attraction.status} isActive={attraction.isActive} /></div>{message && <p className="success-message" role="status">{message}</p>}<AttractionForm key={attraction.id} categories={categories} initialValues={attraction} onSubmit={submit} onCancel={() => navigate('/provider/attractions')} isSubmitting={saving} serverError={error} submitLabel="Save changes" /><div className="sub-navigation"><Link to={`/provider/attractions/${id}/schedules`}>Manage schedules</Link><Link to={`/provider/attractions/${id}/slots`}>Manage experience slots</Link><Link to={`/provider/attractions/${id}/availability`}>View availability</Link></div></section>
}
