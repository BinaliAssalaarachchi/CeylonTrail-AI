import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/useAuth'

function getErrorMessage(error) {
  return error.response?.data?.message || 'Unable to sign in. Check your credentials and try again.'
}

export default function LoginPage() {
  const { isAuthenticated, isLoading, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [form, setForm] = useState({ email: '', password: '' })
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  if (!isLoading && isAuthenticated) return <Navigate to="/" replace />

  function updateField(event) {
    setForm((current) => ({ ...current, [event.target.name]: event.target.value }))
  }

  async function handleSubmit(event) {
    event.preventDefault()
    setError('')
    if (!form.email.trim() || !form.password) {
      setError('Email and password are required.')
      return
    }
    setIsSubmitting(true)
    try {
      await login(form)
      navigate(location.state?.from?.pathname || '/', { replace: true })
    } catch (requestError) {
      setError(getErrorMessage(requestError))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="login-page">
      <section className="login-card" aria-labelledby="login-title">
        <p className="eyebrow">Shared platform access</p>
        <h1 id="login-title">Welcome back</h1>
        <p className="muted">Sign in to continue to CeylonTrail AI.</p>
        <form onSubmit={handleSubmit} noValidate>
          <label htmlFor="email">Email</label>
          <input id="email" name="email" type="email" autoComplete="email" value={form.email} onChange={updateField} required />
          <label htmlFor="password">Password</label>
          <input id="password" name="password" type="password" autoComplete="current-password" value={form.password} onChange={updateField} required />
          {error && <p className="form-error" role="alert">{error}</p>}
          <button className="button button-primary full-width" type="submit" disabled={isSubmitting}>{isSubmitting ? 'Signing in…' : 'Sign in'}</button>
        </form>
      </section>
    </main>
  )
}
