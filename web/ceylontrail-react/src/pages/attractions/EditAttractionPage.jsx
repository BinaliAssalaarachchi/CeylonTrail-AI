import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { getAttractionById, getCategories, updateAttraction } from '../../api/attractions'
import AttractionForm from '../../components/attractions/AttractionForm'
import StatusBadge from '../../components/attractions/StatusBadge'
import { apiErrorMessage } from './attractionUtils'
import AttractionImageManager from '../../components/attractions/AttractionImageManager'

export default function EditAttractionPage() {
  const { id } = useParams()
  const location = useLocation()
  const navigate = useNavigate()
  const [attraction, setAttraction] = useState(null)
  const [categories, setCategories] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [message] = useState(location.state?.message || '')

  useEffect(() => {
    Promise.all([getAttractionById(id), getCategories()])
      .then(([item, categoryList]) => {
        setAttraction(item)
        setCategories(categoryList)
      })
      .catch((e) => setError(apiErrorMessage(e, 'Unable to load this attraction.')))
      .finally(() => setLoading(false))
  }, [id])

  const refreshAttraction = useCallback(async () => {
    const updated = await getAttractionById(id)
    setAttraction(updated)
  }, [id])

  async function submit(data) {
    setSaving(true)
    setError('')
    try {
      await updateAttraction(id, data)
      const resubmitMsg =
        attraction?.status === 'Rejected'
          ? 'Attraction updated and automatically resubmitted for admin review!'
          : 'Attraction updated successfully.'
      navigate('/provider/attractions', { state: { message: resubmitMsg } })
    } catch (e) {
      setError(apiErrorMessage(e, 'Unable to update attraction.'))
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return (
      <section className="page-section">
        <div className="state-card" role="status">
          <strong>Loading attraction…</strong>
          <span>Preparing the management form.</span>
        </div>
      </section>
    )
  }

  if (error && !attraction) {
    return (
      <section className="page-section">
        <div className="state-card state-error" role="alert">
          <strong>{error}</strong>
          <span>We could not load this attraction.</span>
        </div>
        <Link className="button button-secondary-light" to="/provider/attractions">
          Back to attractions
        </Link>
      </section>
    )
  }

  const isRejected = attraction.status === 'Rejected'

  return (
    <section className="page-section form-page">
      <Link className="back-link" to="/provider/attractions">
        ← My attractions
      </Link>

      <div className="consistent-page-header page-header">
        <div>
          <p className="eyebrow">Provider workspace</p>
          <h1>Edit attraction</h1>
          <p className="lead">Update the information visitors and administrators use for this attraction.</p>
        </div>
        <StatusBadge status={attraction.status} isActive={attraction.isActive} />
      </div>

      {message && <p className="success-message" role="status">{message}</p>}

      {/* Admin Rejection Callout Box */}
      {isRejected && attraction.rejectionReason && (
        <div className="provider-edit-rejection-box" role="alert">
          <div className="rejection-box-icon">⚠️</div>
          <div className="rejection-box-content">
            <strong>Administrator Rejection Feedback:</strong>
            <p>{attraction.rejectionReason}</p>
            <small>
              Review the feedback above, make the necessary corrections, and click <strong>"Save changes"</strong> to automatically resubmit your attraction for administrator review.
            </small>
          </div>
        </div>
      )}

      <AttractionImageManager attraction={attraction} onUpdated={refreshAttraction} />

      <AttractionForm
        key={attraction.id}
        categories={categories}
        initialValues={attraction}
        onSubmit={submit}
        onCancel={() => navigate('/provider/attractions')}
        isSubmitting={saving}
        serverError={error}
        submitLabel={isRejected ? 'Save & Resubmit for Review' : 'Save changes'}
      />

      <div className="sub-navigation">
        <Link to={`/provider/attractions/${id}/schedules`}>Manage schedules</Link>
        <Link to={`/provider/attractions/${id}/slots`}>Manage experience slots</Link>
        <Link to={`/provider/attractions/${id}/availability`}>View availability</Link>
      </div>
    </section>
  )
}
