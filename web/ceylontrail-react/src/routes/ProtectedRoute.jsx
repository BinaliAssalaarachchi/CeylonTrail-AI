import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../context/useAuth'

export default function ProtectedRoute({ allowedRoles }) {
  const { isAuthenticated, isLoading, user } = useAuth()
  const location = useLocation()
  if (isLoading) return <div className="route-status">Restoring your session…</div>
  if (!isAuthenticated) return <Navigate to="/login" replace state={{ from: location }} />
  if (allowedRoles && !allowedRoles.includes(user.role)) return <Navigate to="/unauthorized" replace />
  return <Outlet />
}
