import { useEffect, useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import BrandLockup from '../components/BrandLockup'
import { loginSlides } from '../data/destinationImages'

function getErrorMessage(error) { return error.response?.data?.message || 'Unable to sign in. Check your credentials and try again.' }

function LoginDestinationSlideshow() {
  const [activeIndex, setActiveIndex] = useState(0)
  const activeSlide = loginSlides[activeIndex]

  useEffect(() => {
    loginSlides.slice(1, 3).forEach((_, offset) => {
      const nextSlide = loginSlides[(activeIndex + offset + 1) % loginSlides.length]
      const image = new Image()
      image.src = nextSlide.image
    })
  }, [activeIndex])

  useEffect(() => {
    const timer = window.setInterval(() => setActiveIndex((index) => (index + 1) % loginSlides.length), 5600)
    return () => window.clearInterval(timer)
  }, [])

  return <div className="login-slideshow" aria-label={`${activeSlide.destination}, ${activeSlide.region}`}>
    {loginSlides.map((slide, index) => <img key={slide.image} className={`login-slide ${index === activeIndex ? 'is-active' : ''}`} src={slide.image} alt={`${slide.destination}, ${slide.region}`} loading={index === activeIndex ? 'eager' : 'lazy'} decoding="async" fetchPriority={index === activeIndex ? 'high' : 'low'} style={{ objectPosition: slide.position }} />)}
    <div className="login-slide-overlay" aria-hidden="true" />
    <div className="login-slide-meta"><span>CeylonTrail · Sri Lanka</span><strong>{activeSlide.destination} · {activeSlide.region}</strong></div>
    <div className="login-slide-indicators" aria-label="Choose destination image">{loginSlides.map((slide, index) => <button key={slide.image} type="button" className={index === activeIndex ? 'is-active' : ''} onClick={() => setActiveIndex(index)} aria-label={`Show ${slide.destination}`} aria-pressed={index === activeIndex} />)}</div>
  </div>
}

export default function LoginPage() {
  const { isAuthenticated, isLoading, login } = useAuth(); const navigate = useNavigate(); const location = useLocation()
  const [form, setForm] = useState({ email: '', password: '' }); const [error, setError] = useState(''); const [isSubmitting, setIsSubmitting] = useState(false)
  if (!isLoading && isAuthenticated) return <Navigate to="/" replace />
  function updateField(event) { setForm((current) => ({ ...current, [event.target.name]: event.target.value })) }
  async function handleSubmit(event) {
    event.preventDefault(); setError('')
    if (!form.email.trim() || !form.password) { setError('Email and password are required.'); return }
    setIsSubmitting(true)
    try { await login(form); navigate(location.state?.from?.pathname || '/', { replace: true }) } catch (requestError) { setError(getErrorMessage(requestError)) } finally { setIsSubmitting(false) }
  }
  return <main className="login-page">
    <section className="login-visual" aria-label="CeylonTrail Sri Lanka introduction"><LoginDestinationSlideshow /><div className="login-visual-copy"><h1>Shape unforgettable journeys.</h1><p>Manage experiences, coordinate travel and keep every journey moving smoothly.</p></div></section>
    <section className="login-card" aria-labelledby="login-title"><BrandLockup /><p className="eyebrow login-context-label">Staff portal</p><h2 id="login-title">Welcome back</h2><p className="muted">Sign in to your CeylonTrail account.</p><form onSubmit={handleSubmit} noValidate><label htmlFor="email">Email address</label><input id="email" name="email" type="email" autoComplete="email" placeholder="Enter your email address" value={form.email} onChange={updateField} required /><label htmlFor="password">Password</label><input id="password" name="password" type="password" autoComplete="current-password" placeholder="Enter your password" value={form.password} onChange={updateField} required />{error && <p className="form-error" role="alert">{error}</p>}<button className="button button-primary full-width" type="submit" disabled={isSubmitting}>{isSubmitting ? 'Signing in…' : 'Sign in'} <span aria-hidden="true">→</span></button></form></section>
  </main>
}
