import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { createAttraction, getCategories } from '../../api/attractions'
import AttractionForm from '../../components/attractions/AttractionForm'
import { apiErrorMessage } from './attractionUtils'

export default function CreateAttractionPage() {
  const navigate = useNavigate(); const [categories, setCategories] = useState([]); const [loading, setLoading] = useState(true); const [submitting, setSubmitting] = useState(false); const [error, setError] = useState('')
  useEffect(() => { getCategories().then(setCategories).catch((e) => setError(apiErrorMessage(e, 'Unable to load categories.'))).finally(() => setLoading(false)) }, [])
  async function submit(data) { setSubmitting(true); setError(''); try { const created = await createAttraction(data); navigate(`/provider/attractions/${created.id}/edit`, { state: { message: 'Attraction created. It is pending administrator approval.' } }) } catch (e) { setError(apiErrorMessage(e, 'Unable to create attraction.')) } finally { setSubmitting(false) } }
  return <section className="page-section form-page"><Link className="back-link" to="/provider/attractions">← My attractions</Link><div className="consistent-page-header"><p className="eyebrow">Provider workspace</p><h1>Create attraction</h1><p className="lead">Submit an attraction for administrator approval.</p></div>{loading ? <div className="state-card" role="status"><strong>Loading categories…</strong><span>Preparing the attraction form.</span></div> : <AttractionForm categories={categories} onSubmit={submit} onCancel={() => navigate('/provider/attractions')} isSubmitting={submitting} serverError={error} submitLabel="Create attraction" />}</section>
}
