import { Link } from 'react-router-dom'

export default function UnauthorizedPage() {
  return (
    <main className="login-page">
      <section className="login-card">
        <p className="eyebrow">Access restricted</p>
        <h1>Unauthorized</h1>
        <p className="muted">Your account does not have access to that area.</p>
        <Link className="button button-primary full-width" to="/">Return to overview</Link>
      </section>
    </main>
  )
}
