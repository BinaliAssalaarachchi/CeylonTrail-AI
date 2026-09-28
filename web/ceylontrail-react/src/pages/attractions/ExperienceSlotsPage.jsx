import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { createExperienceSlot, deleteExperienceSlot, getAttractionById, updateExperienceSlot } from '../../api/attractions'
import SlotForm from '../../components/attractions/SlotForm'
import { apiErrorMessage, timeValue } from './attractionUtils'
import ConfirmationModal from '../../components/ConfirmationModal'

function readableDate(value) { return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric' }).format(new Date(`${value}T00:00:00`)) }

export default function ExperienceSlotsPage() {
  const { id } = useParams()
  const [attraction, setAttraction] = useState(null)
  const [editing, setEditing] = useState(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [formError, setFormError] = useState('')
  const [message, setMessage] = useState('')
  const [confirming, setConfirming] = useState(null)
  const [deleting, setDeleting] = useState(false)

  const load = useCallback(async () => {
    try {
      setAttraction(await getAttractionById(id))
    } catch (e) {
      setError(apiErrorMessage(e, 'Unable to load experience slots.'))
    } finally {
      setLoading(false)
    }
  }, [id])

  useEffect(() => {
    load()
  }, [load])

  function initial(slot) {
    return {
      date: slot.date,
      startTime: timeValue(slot.startTime),
      endTime: timeValue(slot.endTime),
      capacity: slot.capacity,
      availableCapacity: slot.availableCapacity,
    }
  }

  async function save(data) {
    setSaving(true)
    setError('')
    setFormError('')
    setMessage('')
    try {
      if (editing) await updateExperienceSlot(id, editing.id, data)
      else await createExperienceSlot(id, data)
      setEditing(null)
      setMessage(editing ? 'Slot updated successfully.' : 'Slot created successfully.')
      await load()
    } catch (e) {
      setFormError(apiErrorMessage(e, 'Unable to save slot.'))
    } finally {
      setSaving(false)
    }
  }

  async function remove(slot) {
    setDeleting(true)
    setError('')
    setFormError('')
    setMessage('')
    try {
      await deleteExperienceSlot(id, slot.id)
      setConfirming(null)
      setMessage('Slot deleted successfully.')
      await load()
    } catch (e) {
      setError(apiErrorMessage(e, 'Unable to delete slot.'))
      setConfirming(null)
    } finally {
      setDeleting(false)
    }
  }

  if (loading) {
    return (
      <section className="page-section">
        <div className="state-card" role="status">
          <strong>Loading experience slots…</strong>
          <span>Preparing the session workspace.</span>
        </div>
      </section>
    )
  }

  return (
    <section className="page-section wide-page operations-page">
      <Link className="back-link" to={`/provider/attractions/${id}/edit`}>
        ← {attraction?.name || 'Attraction'}
      </Link>
      <div className="consistent-page-header">
        <p className="eyebrow">Provider workspace · Experience operations</p>
        <h1>Experience slots</h1>
        <p className="lead">Manage dated sessions and their remaining capacity.</p>
      </div>

      {message && <p className="success-message" role="status">{message}</p>}
      {error && <p className="form-error notice-error" role="alert">{error}</p>}

      <section className="panel operations-form-panel">
        <div className="section-heading">
          <div>
            <p className="eyebrow">{editing ? 'Update slot' : 'Add slot'}</p>
            <h2>{editing ? 'Edit experience session' : 'Create experience session'}</h2>
          </div>
          {editing && (
            <button className="button button-secondary-light" type="button" onClick={() => { setEditing(null); setFormError(''); }}>
              Cancel edit
            </button>
          )}
        </div>
        <SlotForm
          key={editing?.id || 'new'}
          initialValues={editing ? initial(editing) : undefined}
          onSubmit={save}
          onCancel={editing ? () => { setEditing(null); setFormError(''); } : null}
          isSubmitting={saving}
          serverError={formError}
        />
      </section>

      <section className="list-panel operations-list-panel">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Scheduled sessions</p>
            <h2>Existing slots</h2>
          </div>
          <span className="record-count">{attraction?.experienceSlots?.length || 0} sessions</span>
        </div>

        {!attraction?.experienceSlots?.length ? (
          <div className="state-empty">
            <strong>No experience slots have been configured.</strong>
            <span>Add a dated session above to begin managing capacity.</span>
          </div>
        ) : (
          attraction.experienceSlots.map((slot) => (
            <div className="operation-row slot-row" key={slot.id}>
              <div className="operation-row-main">
                <strong>{readableDate(slot.date)}</strong>
                <span>{timeValue(slot.startTime)} – {timeValue(slot.endTime)}</span>
              </div>
              <div className="slot-capacity">
                <span>
                  <small>Capacity</small>
                  <b>{slot.capacity}</b>
                </span>
                <span>
                  <small>Available</small>
                  <b>{slot.availableCapacity}</b>
                </span>
              </div>
              <div className="row-actions">
                <button className="text-button" type="button" onClick={() => { setEditing(slot); setFormError(''); }}>
                  Edit
                </button>
                <button className="text-button danger-text" type="button" onClick={() => setConfirming(slot)}>
                  Delete
                </button>
              </div>
            </div>
          ))
        )}
      </section>

      {confirming && (
        <ConfirmationModal
          title="Remove this experience slot?"
          message={`${readableDate(confirming.date)} · ${timeValue(confirming.startTime)} – ${timeValue(confirming.endTime)}. Removing it will delete this session and its capacity record.`}
          confirmLabel="Remove slot"
          isLoading={deleting}
          onConfirm={() => remove(confirming)}
          onClose={() => setConfirming(null)}
        />
      )}
    </section>
  )
}
