import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { deleteAttraction, getMyAttractions } from '../../api/attractions'
import AttractionCard from '../../components/attractions/AttractionCard'
import { apiErrorMessage } from './attractionUtils'

export default function MyAttractionsPage() {
  const [result, setResult] = useState(null); const [error, setError] = useState(''); const [loading, setLoading] = useState(true); const [message, setMessage] = useState('')
  const load = useCallback(async () => { setLoading(true); setError(''); try { setResult(await getMyAttractions({ page: 1, pageSize: 100 })) } catch (requestError) { setError(apiErrorMessage(requestError, 'Unable to load your attractions.')) } finally { setLoading(false) } }, [])
  useEffect(() => { load() }, [load])
  async function deactivate(attraction) { if (!window.confirm(`Deactivate “${attraction.name}”? It will no longer be publicly available.`)) return; try { await deleteAttraction(attraction.id); setMessage('Attraction deactivated successfully.'); load() } catch (requestError) { setError(apiErrorMessage(requestError)) } }
  return <section className="page-section wide-page"><div className="page-header"><div><p className="eyebrow">Provider workspace</p><h1>My Attractions</h1><p className="lead">Manage the attractions and experiences you provide.</p></div><Link className="button button-primary" to="/provider/attractions/create">Create attraction</Link></div>{message && <p className="success-message" role="status">{message}</p>}{loading && <p className="notice">Loading attractions…</p>}{error && <p className="form-error notice-error" role="alert">{error}</p>}{!loading && !error && !result?.items?.length && <div className="empty-state"><h2>No attractions yet</h2><p>Create your first attraction to begin managing schedules and experience slots.</p><Link className="button button-primary" to="/provider/attractions/create">Create your first attraction</Link></div>}<div className="management-grid">{result?.items?.map((attraction) => <AttractionCard key={attraction.id} attraction={attraction} onDeactivate={deactivate} />)}</div></section>
}
