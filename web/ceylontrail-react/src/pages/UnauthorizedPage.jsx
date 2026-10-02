import { Link } from 'react-router-dom'
import BrandLockup from '../components/BrandLockup'

export default function UnauthorizedPage() {
  return <main className="login-page login-page-compact"><section className="login-card" aria-labelledby="unauthorized-title"><BrandLockup /><p className="eyebrow login-context-label">Staff portal</p><h1 id="unauthorized-title">This area isn't available for your account</h1><p className="muted">Your account does not have access to this area. Return to your overview to continue.</p><Link className="button button-primary full-width" to="/">Return to overview <span aria-hidden="true">→</span></Link></section></main>
}
