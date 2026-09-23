import { Link } from 'react-router-dom'
import { useAuth } from '../context/useAuth'

const roleLinks = {
  TourismProvider: { label: 'Open provider workspace', to: '/provider' },
  TravelCoordinator: { label: 'Open coordinator workspace', to: '/coordinator' },
  Administrator: { label: 'Open administrator workspace', to: '/administrator' },
}

const modules = [
  { label: 'Bookings', to: '/bookings', description: 'Keep reservations and travel arrangements connected.', icon: '□' },
  { label: 'Trips & Itineraries', to: '/trip-planning', description: 'Plan and shape journeys around the island.', icon: '↗' },
  { label: 'Discover', to: '/discover', description: 'Explore places, experiences and cultural routes.', icon: '◉' },
  { label: 'Travel Operations', to: '/travel-alerts', description: 'Coordinate alerts and operational intelligence.', icon: '✣' },
  { label: 'AI Operations', to: '/ai-operations', description: 'Intelligence workflows and itinerary validations.', icon: '✦' },
]

export default function HomePage() {
  const { user } = useAuth()
  const roleLink = roleLinks[user.role]

  return (
    <section className="dashboard-page" aria-labelledby="dashboard-title">
      <div className="dashboard-hero">
        <div className="hero-content">
          <p className="eyebrow eyebrow-on-dark">CeylonTrail workspace</p>
          <h1 id="dashboard-title">Ayubowan, {user.firstName}.</h1>
          <p className="hero-lead">A calm, shared foundation for building thoughtful journeys across Sri Lanka.</p>
          <div className="hero-chip"><span className="signal-dot signal-dot-light" /> Signed in as {user.role}</div>
        </div>
        <div className="hero-orbit" aria-hidden="true"><span /><span /><span /></div>
      </div>

      <div className="dashboard-intro">
        <div>
          <p className="eyebrow">Your workspace</p>
          <h2>What would you like to shape today?</h2>
          <p className="muted">Choose your role workspace below, or browse any of the platform modules across the platform.</p>
        </div>
        {roleLink && <Link className="button button-primary" to={roleLink.to}>{roleLink.label} <span aria-hidden="true">→</span></Link>}
      </div>

      <div className="module-grid" aria-label="CeylonTrail platform modules">
        {modules.map((module) => (
          <Link
            to={module.to}
            className="module-card module-card-active"
            key={module.label}
            style={{ textDecoration: 'none', color: 'inherit' }}
          >
            <span className="module-icon" aria-hidden="true">{module.icon}</span>
            <div>
              <h3>{module.label}</h3>
              <p>{module.description}</p>
            </div>
            <span className="module-status" style={{ color: 'var(--color-tea, #2d6a4f)', fontWeight: 600 }}>Active · Open Workspace →</span>
          </Link>
        ))}
      </div>

      <div className="foundation-callout">
        <div className="callout-symbol" aria-hidden="true">✦</div>
        <div>
          <p className="eyebrow">A considered beginning</p>
          <h2>Designed for the island, ready for the journey.</h2>
          <p>All modules are fully connected to your active profile across CeylonTrail.</p>
        </div>
      </div>
    </section>
  )
}
