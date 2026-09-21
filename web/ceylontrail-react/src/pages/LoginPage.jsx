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
      <section className="login-visual" aria-label="CeylonTrail introduction">
        <div className="login-visual-copy">
          <p className="eyebrow eyebrow-on-dark">Tropical heritage intelligence</p>
          <h1>Every route begins with a sense of place.</h1>
          <p>One shared workspace for the people shaping meaningful journeys across Sri Lanka.</p>
          <div className="login-values" aria-label="CeylonTrail values">
            <span><strong aria-hidden="true">♧</strong><b>People</b><small>Stronger communities</small></span>
            <span><strong aria-hidden="true">⌂</strong><b>Places</b><small>Richer experiences</small></span>
            <span><strong aria-hidden="true">≋</strong><b>A brighter tomorrow</b><small>Thoughtful journeys</small></span>
          </div>
        </div>
      </section>
      <section className="login-card" aria-labelledby="login-title">
        <div className="login-brand">
          <svg className="login-brand-symbol" viewBox="0 0 80 80" aria-hidden="true">
            <path className="logo-leaf" d="M40 70C17 61 10 39 20 12c24 4 37 19 33 38-2 9-7 15-13 20Z" />
            <path className="logo-leaf-vein" d="M25 20c10 13 16 28 16 45" />
          </svg>
          <span><strong>CeylonTrail</strong><small>Intelligence platform</small></span>
        </div>
        <p className="eyebrow">Shared platform access</p>
        <h2 id="login-title">Welcome back</h2>
        <p className="muted">Sign in to continue to your workspace.</p>
        <form onSubmit={handleSubmit} noValidate>
          <label htmlFor="email">Email address</label>
          <input id="email" name="email" type="email" autoComplete="email" placeholder="Enter your email address" value={form.email} onChange={updateField} required />
          <label htmlFor="password">Password</label>
          <input id="password" name="password" type="password" autoComplete="current-password" placeholder="Enter your password" value={form.password} onChange={updateField} required />
          {error && <p className="form-error" role="alert">{error}</p>}
          <button className="button button-primary full-width" type="submit" disabled={isSubmitting}>{isSubmitting ? 'Signing in…' : 'Sign in'} <span aria-hidden="true">→</span></button>
        </form>
      </section>
    </main>
  )
}
