import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../context/useAuth'

const roleLinks = {
  TourismProvider: { label: 'Provider workspace', to: '/provider' },
  TravelCoordinator: { label: 'Coordinator workspace', to: '/coordinator' },
  Administrator: { label: 'Administrator workspace', to: '/administrator' },
}

const _platformModules = [
  { label: 'Bookings', to: '/bookings', icon: '□' },
  { label: 'Trips & Itineraries', to: '/trip-planning', icon: '↗' },
  { label: 'Discover', to: '/discover', icon: '◉' },
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
  const canManageOperations = ['TravelCoordinator', 'Administrator'].includes(user.role)
  const canViewAdvisories = ['Tourist', 'TourismProvider'].includes(user.role)
  const attractionRoute = user.role === 'TourismProvider' ? '/provider/attractions' : user.role === 'Administrator' ? '/admin/attractions' : null
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
          {canManageOperations && <NavLink className="nav-link" to="/trip-planning"><span className="nav-icon">TR</span><span>Trip planning</span></NavLink>}
          <div className="nav-divider" /><p className="nav-section-label">Platform modules</p>
          <NavLink className="nav-link" to="/bookings"><span className="nav-icon">BK</span><span>Bookings</span></NavLink>
          {canViewAdvisories && <NavLink className="nav-link" to="/travel-advisories"><span className="nav-icon">TA</span><span>Travel Advisories</span></NavLink>}
          {canManageOperations && <><NavLink className="nav-link" to="/travel-alerts"><span className="nav-icon">OP</span><span>Travel Operations</span></NavLink><NavLink className="nav-link" to="/ai-operations"><span className="nav-icon">AI</span><span>AI Operations</span></NavLink></>}
          {attractionRoute ? <NavLink className="nav-link" to={attractionRoute}><span className="nav-icon">AT</span><span>Discover / Attractions</span></NavLink> : <span className="nav-link nav-link-disabled" aria-disabled="true"><span className="nav-icon">AT</span><span>Discover</span></span>}
        </nav>
        <div className="sidebar-footer">
          <div className="protocol-note"><span className="protocol-icon">◬</span><span><strong>Shared platform</strong><small>All workspace modules unlocked.</small></span></div>
          <small>© 2026 CeylonTrail</small>
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
