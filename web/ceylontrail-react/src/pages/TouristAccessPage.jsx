import { useAuth } from '../context/useAuth'

export default function TouristAccessPage() {
  const { user, logout } = useAuth()

  return (
    <main className="login-page login-page-compact">
      <section className="login-card" aria-labelledby="tourist-access-title">
        <div className="login-brand"><span className="brand-mark" aria-hidden="true">✦</span><span><strong>CeylonTrail</strong><small>Traveller access</small></span></div>
        <p className="eyebrow">Traveller account</p>
        <h1 id="tourist-access-title">Use the CeylonTrail traveller app</h1>
        <p className="muted">Hi {user.firstName}. The web portal is for tourism providers, travel coordinators, and administrators. Use the CeylonTrail traveller/mobile application for trips, itineraries, travel guidance, and bookings.</p>
        <button className="button button-primary full-width" type="button" onClick={logout}>Log out</button>
      </section>
    </main>
  )
}
