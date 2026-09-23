import { Link } from 'react-router-dom'
import { useAuth } from '../context/useAuth'

const roleLinks = { TourismProvider: { label: 'Open provider workspace', to: '/provider' }, TravelCoordinator: { label: 'Open coordinator workspace', to: '/coordinator' }, Administrator: { label: 'Open administrator workspace', to: '/administrator' } }
const modules = [
  { label: 'Trips & Itineraries', description: 'Plan and shape journeys around the island.', icon: 'TR' },
  { label: 'Discover', description: 'Explore places, experiences and cultural routes.', icon: 'AT' },
  { label: 'Bookings', description: 'Keep reservations and travel arrangements connected.', icon: 'BK' },
  { label: 'Travel Operations', description: 'Coordinate alerts and operational intelligence.', icon: 'OP' },
  { label: 'AI Operations', description: 'A shared space for intelligence workflows.', icon: 'AI' },
]

export default function HomePage() {
  const { user } = useAuth()
  const roleLink = roleLinks[user.role]
  const attractionRoute = user.role === 'TourismProvider' ? '/provider/attractions' : user.role === 'Administrator' ? '/admin/attractions' : null
  return <section className="dashboard-page" aria-labelledby="dashboard-title"><div className="dashboard-hero"><div className="hero-content"><p className="eyebrow eyebrow-on-dark">CeylonTrail workspace</p><h1 id="dashboard-title">Ayubowan, {user.firstName}.</h1><p className="hero-lead">A calm, shared foundation for building thoughtful journeys across Sri Lanka.</p><div className="hero-chip"><span className="signal-dot signal-dot-light" /> Signed in as {user.role}</div></div><div className="hero-orbit" aria-hidden="true"><span /><span /><span /></div></div><div className="dashboard-intro"><div><p className="eyebrow">Your workspace</p><h2>What would you like to shape today?</h2><p className="muted">Choose your role workspace below, or open a live platform module.</p></div>{roleLink && <Link className="button button-primary" to={roleLink.to}>{roleLink.label} -&gt;</Link>}</div><div className="module-grid" aria-label="CeylonTrail platform modules">{modules.map((module) => { const route = module.label === 'Bookings' ? '/bookings' : module.label === 'Discover' ? attractionRoute : module.label === 'Travel Operations' && ['TravelCoordinator', 'Administrator'].includes(user.role) ? '/travel-alerts' : module.label === 'AI Operations' && ['TravelCoordinator', 'Administrator'].includes(user.role) ? '/ai-operations' : null; return route ? <Link to={route} className="module-card module-card-active" key={module.label} style={{ textDecoration: 'none', color: 'inherit' }}><span className="module-icon" aria-hidden="true">{module.icon}</span><div><h3>{module.label === 'Discover' && attractionRoute ? 'Attractions' : module.label}</h3><p>{module.description}</p></div><span className="module-status" style={{ color: 'var(--color-tea)', fontWeight: 600 }}>Active - Open workspace -&gt;</span></Link> : <article className="module-card" key={module.label}><span className="module-icon" aria-hidden="true">{module.icon}</span><div><h3>{module.label}</h3><p>{module.description}</p></div><span className="module-status">Foundation ready</span></article> })}</div><div className="foundation-callout"><div className="callout-symbol" aria-hidden="true">AI</div><div><p className="eyebrow">A considered beginning</p><h2>Designed for the island, ready for the journey.</h2><p>The shared shell is in place for the project teams to add their domain experiences without losing the CeylonTrail sense of place.</p></div></div></section>
}
