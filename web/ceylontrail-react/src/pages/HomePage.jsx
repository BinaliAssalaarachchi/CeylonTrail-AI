import { useAuth } from '../context/useAuth'

export default function HomePage() {
  const { user } = useAuth()
  const isTourist = user.role === 'Tourist'
  return (
    <section className="page-section">
      <p className="eyebrow">Application overview</p>
      <h1>Good to see you, {user.firstName}.</h1>
      <p className="lead">You are signed in as <strong>{user.role}</strong>.</p>
      {isTourist && <p className="notice">Tourist experiences will be provided through the Flutter application. This web shell is ready for shared authentication.</p>}
      {!isTourist && <p className="notice">Your role-based dashboard is available from the navigation.</p>}
    </section>
  )
}
