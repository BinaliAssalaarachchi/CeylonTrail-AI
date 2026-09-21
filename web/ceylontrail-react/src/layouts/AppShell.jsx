import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../context/useAuth'

const roleLinks = {
  TourismProvider: { label: 'Provider workspace', to: '/provider' },
  TravelCoordinator: { label: 'Coordinator workspace', to: '/coordinator' },
  Administrator: { label: 'Administrator workspace', to: '/administrator' },
}

const futureModules = [
  { label: 'Trips & Itineraries', icon: '↗' },
  { label: 'Discover', icon: '◉' },
  { label: 'Bookings', icon: '□' },
  { label: 'Travel Operations', icon: '✣' },
  { label: 'AI Operations', icon: '✦' },
]

function BrandMark() {
  return (
    <div className="brand-lockup">
      <svg className="brand-mark" viewBox="0 0 80 80" aria-hidden="true">
        <path className="brand-leaf" d="M40 70C17 61 10 39 20 12c24 4 37 19 33 38-2 9-7 15-13 20Z" />
        <path className="brand-leaf-vein" d="M25 20c10 13 16 28 16 45" />
      </svg>
      <span className="brand-copy">
        <span className="brand-name">CeylonTrail</span>
        <span className="brand-subtitle">Intelligence platform</span>
      </span>
    </div>
  )
}

export default function AppShell() {
  const { user, logout } = useAuth()
  const roleLink = roleLinks[user.role]
  const canMonitorTrips = ['TravelCoordinator', 'Administrator'].includes(user.role)

  return (
    <div className="app-shell">
      <aside className="sidebar" aria-label="Application navigation">
        <NavLink className="brand-link" to="/" aria-label="CeylonTrail home"><BrandMark /></NavLink>
        <div className="workspace-note">
          <span className="workspace-note-label"><span className="signal-dot" /> Shared workspace</span>
          <span>Travel intelligence for the CeylonTrail team.</span>
        </div>
        <nav className="primary-nav">
          <NavLink className="nav-link" to="/"><span className="nav-icon">⌂</span><span>Overview / Dashboard</span></NavLink>
          {roleLink && <NavLink className="nav-link" to={roleLink.to}><span className="nav-icon">◇</span><span>{roleLink.label}</span></NavLink>}
          {canMonitorTrips && <NavLink className="nav-link" to="/trip-planning"><span className="nav-icon">↗</span><span>Trip planning</span></NavLink>}
          <div className="nav-divider" />
          <p className="nav-section-label">Platform modules</p>
          {futureModules.map((module) => (
            <span className="nav-link nav-link-disabled" key={module.label} aria-disabled="true" title="Available in a future feature phase">
              <span className="nav-icon">{module.icon}</span><span>{module.label}</span>
            </span>
          ))}
        </nav>
        <div className="sidebar-footer">
          <div className="protocol-note"><span className="protocol-icon">◌</span><span><strong>Shared foundation</strong><small>Feature workspaces will appear here.</small></span></div>
          <small>© 2025 CeylonTrail</small>
        </div>
      </aside>
      <div className="shell-content">
        <header className="topbar">
          <div className="topbar-context"><span className="topbar-kicker">CeylonTrail AI</span><span className="topbar-separator">/</span><span>Workspace overview</span></div>
          <div className="account-summary">
            <div className="account-copy"><strong>{user.firstName} {user.lastName}</strong><span>{user.role}</span></div>
            <span className="avatar" aria-hidden="true">{user.firstName?.[0]}{user.lastName?.[0]}</span>
            <button className="icon-button" type="button" onClick={logout} aria-label="Log out" title="Log out">↪</button>
          </div>
        </header>
        <main className="main-content"><Outlet /></main>
      </div>
    </div>
  )
}
