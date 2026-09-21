import { Link } from 'react-router-dom'

export default function UnauthorizedPage() {
  return (
    <main className="login-page login-page-compact">
      <section className="login-card" aria-labelledby="unauthorized-title">
        <div className="login-brand"><span className="brand-mark" aria-hidden="true">✦</span><span><strong>CeylonTrail</strong><small>Intelligence platform</small></span></div>
        <p className="eyebrow">Access restricted</p>
        <h1 id="unauthorized-title">This path is not yours yet.</h1>
        <p className="muted">Your account does not have access to that workspace.</p>
        <Link className="button button-primary full-width" to="/">Return to overview <span aria-hidden="true">→</span></Link>
      </section>
    </main>
  )
}
