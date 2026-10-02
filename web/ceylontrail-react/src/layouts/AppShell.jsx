import { useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import TouristAccessPage from '../pages/TouristAccessPage'
import BrandLockup from '../components/BrandLockup'

const roleName = (role) => role === 'TourismProvider' ? 'Tourism Provider' : role === 'TravelCoordinator' ? 'Travel Coordinator' : 'Administrator'

const exactNavigationRoutes = new Set(['/provider', '/coordinator', '/administrator', '/bookings', '/travel-advisories', '/travel-alerts', '/ai-operations', '/discover'])

const navigationForRole = {
  TourismProvider: [
    { to: '/provider', label: 'Overview' },
    { to: '/provider/attractions', label: 'My experiences' },
    { to: '/bookings', label: 'Bookings' },
    { to: '/travel-advisories', label: 'Travel advisories' },
  ],
  TravelCoordinator: [
    { to: '/coordinator', label: 'Overview' },
    { to: '/bookings', label: 'Bookings' },
    { to: '/trip-planning', label: 'Trips & itineraries' },
    { to: '/travel-alerts', label: 'Travel operations' },
    { to: '/ai-operations', label: 'AI operations' },
    { to: '/discover', label: 'Discover' },
  ],
  Administrator: [
    { to: '/administrator', label: 'Overview' },
    { to: '/bookings', label: 'Bookings' },
    { to: '/trip-planning', label: 'Trips & itineraries' },
    { to: '/travel-alerts', label: 'Travel operations' },
    { to: '/ai-operations', label: 'AI operations' },
    { to: '/admin/attractions', label: 'Attraction review' },
  ],
}

function pageTitleFor(pathname) {
  if (pathname === '/') return 'Overview'
  if (pathname.startsWith('/ai-operations')) return 'AI Operations'
  if (pathname.startsWith('/bookings')) return 'Bookings'
  if (pathname.startsWith('/trip-planning')) return 'Trips & itineraries'
  if (pathname.startsWith('/travel-alerts')) return 'Travel operations'
  if (pathname.startsWith('/travel-advisories')) return 'Travel advisories'
  if (pathname.startsWith('/provider/attractions')) return 'My experiences'
  if (pathname.startsWith('/admin/attractions')) return 'Attraction review'
  if (pathname.startsWith('/discover')) return 'Discover'
  return 'CeylonTrail'
}

export default function AppShell() {
  const { user, logout } = useAuth()
  const location = useLocation()
  const [menuOpen, setMenuOpen] = useState(false)
  if (user.role === 'Tourist') return <TouristAccessPage />

  const role = roleName(user.role)
  const navItems = navigationForRole[user.role] || []
  const closeMenu = () => setMenuOpen(false)

  return <div className="app-shell"><div className={`sidebar-backdrop ${menuOpen ? 'is-visible' : ''}`} onClick={closeMenu} aria-hidden="true" /><aside className={`sidebar ${menuOpen ? 'is-open' : ''}`} aria-label="Application navigation">
    <NavLink className="brand-link" to="/" aria-label="CeylonTrail home" onClick={closeMenu}><BrandLockup light compact /></NavLink>
    <nav className="primary-nav" aria-label={`${role} navigation`}>{navItems.map((item) => <NavLink onClick={closeMenu} className="nav-link" to={item.to} end={exactNavigationRoutes.has(item.to)} key={item.to}><span>{item.label}</span></NavLink>)}</nav>
    <div className="sidebar-footer"><div className="profile-mini"><span className="avatar">{user.firstName?.[0]}{user.lastName?.[0]}</span><span><strong>{user.firstName} {user.lastName}</strong><small>{role}</small></span></div><button className="sidebar-signout" type="button" onClick={logout}>Sign out</button><small>© 2026 CeylonTrail</small></div>
  </aside><div className="shell-content"><header className="topbar"><button className="menu-toggle" type="button" onClick={() => setMenuOpen((value) => !value)} aria-label="Open navigation menu">☰</button><div className="topbar-context"><span className="topbar-kicker">CeylonTrail</span><span className="topbar-separator">/</span><span>{pageTitleFor(location.pathname)}</span></div><div className="account-summary"><div className="account-copy"><strong>{user.firstName} {user.lastName}</strong><span>{role}</span></div><span className="avatar" aria-hidden="true">{user.firstName?.[0]}{user.lastName?.[0]}</span><button className="icon-button" type="button" onClick={logout} aria-label="Log out" title="Log out">↪</button></div></header><main className="main-content"><Outlet /></main></div></div>
}
