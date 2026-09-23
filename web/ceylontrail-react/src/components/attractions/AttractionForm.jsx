import { useState } from 'react'
import { toAttractionForm } from '../../pages/attractions/formUtils'

function validate(form) {
  const errors = {}
  if (!form.categoryId) errors.categoryId = 'Choose a category.'
  for (const field of ['name', 'description', 'district', 'address']) if (!form[field].trim()) errors[field] = 'This field is required.'
  const latitude = Number(form.latitude); const longitude = Number(form.longitude); const price = Number(form.price)
  if (form.latitude === '' || !Number.isFinite(latitude) || latitude < -90 || latitude > 90) errors.latitude = 'Latitude must be between -90 and 90.'
  if (form.longitude === '' || !Number.isFinite(longitude) || longitude < -180 || longitude > 180) errors.longitude = 'Longitude must be between -180 and 180.'
  if (form.price === '' || !Number.isFinite(price) || price < 0) errors.price = 'Price must be zero or greater.'
  return errors
}

export default function AttractionForm({ categories, initialValues, onSubmit, isSubmitting, serverError }) {
  const [form, setForm] = useState(() => toAttractionForm(initialValues))
  const [errors, setErrors] = useState({})

  function updateField(event) {
    setForm((current) => ({ ...current, [event.target.name]: event.target.value }))
    setErrors((current) => ({ ...current, [event.target.name]: '' }))
  }

  function submit(event) {
    event.preventDefault()
    const nextErrors = validate(form)
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length) return
    onSubmit({ ...form, latitude: Number(form.latitude), longitude: Number(form.longitude), price: Number(form.price) })
  }

  const fields = [
    ['name', 'Name', 'text'], ['district', 'District', 'text'], ['address', 'Address', 'text'],
    ['latitude', 'Latitude', 'number'], ['longitude', 'Longitude', 'number'], ['price', 'Price', 'number'],
  ]
  return (
    <form className="form-grid" onSubmit={submit} noValidate>
      <div className="field full-span"><label htmlFor="categoryId">Category</label><select id="categoryId" name="categoryId" value={form.categoryId} onChange={updateField} required><option value="">Select a category</option>{categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select>{errors.categoryId && <small className="field-error">{errors.categoryId}</small>}</div>
      {fields.map(([name, label, type]) => <div className="field" key={name}><label htmlFor={name}>{label}</label><input id={name} name={name} type={type} step={type === 'number' ? 'any' : undefined} value={form[name]} onChange={updateField} required={['name', 'district', 'address'].includes(name)} />{errors[name] && <small className="field-error">{errors[name]}</small>}</div>)}
      <div className="field full-span"><label htmlFor="description">Description</label><textarea id="description" name="description" rows="5" value={form.description} onChange={updateField} required />{errors.description && <small className="field-error">{errors.description}</small>}</div>
      {serverError && <p className="form-error full-span" role="alert">{serverError}</p>}
      <div className="form-actions full-span"><button className="button button-primary" type="submit" disabled={isSubmitting}>{isSubmitting ? 'Saving…' : 'Save attraction'}</button></div>
    </form>
  )
}
