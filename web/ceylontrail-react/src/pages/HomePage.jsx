import { Navigate } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import RoleDashboardPage from './RoleDashboardPage'
import AdminDashboardPage from './admin/AdminDashboardPage'

export default function HomePage() {
  const { user } = useAuth()

  if (user.role === 'TourismProvider') return <RoleDashboardPage />
  if (user.role === 'TravelCoordinator') return <RoleDashboardPage title="Travel Coordinator Dashboard" description="Coordinate journeys, review operational work, and keep traveller plans moving." />
  if (user.role === 'Administrator') return <AdminDashboardPage />
  return <Navigate to="/unauthorized" replace />
}
