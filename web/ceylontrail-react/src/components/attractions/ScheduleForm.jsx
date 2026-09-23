import { useState } from 'react'

const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
const blank = { dayOfWeek: 'Monday', openingTime: '09:00', closingTime: '17:00', isClosed: false }

export default function ScheduleForm({ initialValues = blank, onSubmit, onCancel, isSubmitting, serverError }) {
  const [form, setForm] = useState(initialValues)
  const [error, setError] = useState('')
  function submit(event) {
    event.preventDefault(); setError('')
    if (!form.isClosed && (!form.openingTime || !form.closingTime || form.openingTime >= form.closingTime)) { setError('Closing time must be later than opening time.'); return }
    onSubmit({ ...form, openingTime: form.isClosed ? null : `${form.openingTime}:00`, closingTime: form.isClosed ? null : `${form.closingTime}:00` })
  }
  return <form className="inline-form" onSubmit={submit} noValidate><label>Day<select value={form.dayOfWeek} onChange={(e) => setForm({ ...form, dayOfWeek: e.target.value })}>{days.map((day) => <option key={day}>{day}</option>)}</select></label><label>Opens<input type="time" value={form.openingTime || ''} disabled={form.isClosed} onChange={(e) => setForm({ ...form, openingTime: e.target.value })} /></label><label>Closes<input type="time" value={form.closingTime || ''} disabled={form.isClosed} onChange={(e) => setForm({ ...form, closingTime: e.target.value })} /></label><label className="checkbox-label"><input type="checkbox" checked={form.isClosed} onChange={(e) => setForm({ ...form, isClosed: e.target.checked })} /> Closed</label>{(error || serverError) && <small className="field-error full-span">{error || serverError}</small>}<div className="form-actions"><button className="button button-primary" disabled={isSubmitting}>{isSubmitting ? 'Saving…' : 'Save schedule'}</button>{onCancel && <button className="button button-secondary dark-button" type="button" onClick={onCancel}>Cancel</button>}</div></form>
}
