import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../context/useAuth'

const roleLinks = {
  TourismProvider: { label: 'Provider dashboard', to: '/provider' },
  TravelCoordinator: { label: 'Coordinator dashboard', to: '/coordinator' },
  Administrator: { label: 'Administrator dashboard', to: '/administrator' },
}

export default function AppShell() {
  const { user, logout } = useAuth()
  const roleLink = roleLinks[user.role]
  return (
    <div className="app-shell">
      <header className="topbar">
        <NavLink className="brand" to="/">CeylonTrail AI</NavLink>
        <div className="account-summary">
          <span>{user.firstName} {user.lastName}</span>
          <span className="role-badge">{user.role}</span>
          <button className="button button-secondary" type="button" onClick={logout}>Log out</button>
        </div>
      </header>
      <div className="shell-body">
        <nav className="sidebar" aria-label="Application navigation">
          <NavLink className="nav-link" to="/">Overview</NavLink>
          {roleLink && <NavLink className="nav-link" to={roleLink.to}>{roleLink.label}</NavLink>}
        </nav>
        <main className="main-content"><Outlet /></main>
      </div>
    </div>
  )
}
