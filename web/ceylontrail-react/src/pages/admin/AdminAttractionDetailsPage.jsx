import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { approveAttraction, getAttractionById } from '../../api/attractions'
import StatusBadge from '../../components/attractions/StatusBadge'
import { apiErrorMessage } from '../attractions/attractionUtils'

export default function AdminAttractionDetailsPage() {
  const { id } = useParams(); const [attraction, setAttraction] = useState(null); const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [saving, setSaving] = useState(false); const [message, setMessage] = useState('')
  useEffect(() => { getAttractionById(id).then(setAttraction).catch((e) => setError(apiErrorMessage(e, 'Unable to load attraction.'))).finally(() => setLoading(false)) }, [id])
  async function approve() { setSaving(true); setError(''); try { setAttraction(await approveAttraction(id)); setMessage('Attraction approved successfully.') } catch (e) { setError(apiErrorMessage(e, 'Unable to approve attraction.')) } finally { setSaving(false) } }
  if (loading) return <section className="page-section"><p className="notice">Loading attraction…</p></section>
  if (error && !attraction) return <section className="page-section"><p className="form-error notice-error">{error}</p><Link className="button button-secondary dark-button" to="/admin/attractions">Back to pending attractions</Link></section>
  return <section className="page-section wide-page"><Link className="back-link" to="/admin/attractions">← Pending attractions</Link><div className="page-header"><div><p className="eyebrow">Administrator review</p><h1>{attraction.name}</h1></div><StatusBadge status={attraction.status} isActive={attraction.isActive} /></div>{message && <p className="success-message" role="status">{message}</p>}{error && <p className="form-error notice-error" role="alert">{error}</p>}<div className="detail-panel"><dl><dt>Category</dt><dd>{attraction.category?.name}</dd><dt>Provider ID</dt><dd>{attraction.providerId}</dd><dt>District</dt><dd>{attraction.district}</dd><dt>Address</dt><dd>{attraction.address}</dd><dt>Price</dt><dd>{attraction.price}</dd><dt>Description</dt><dd>{attraction.description}</dd></dl>{attraction.status === 'PendingApproval' && attraction.isActive && <button className="button button-primary" type="button" onClick={approve} disabled={saving}>{saving ? 'Approving…' : 'Approve attraction'}</button>}</div></section>
}
