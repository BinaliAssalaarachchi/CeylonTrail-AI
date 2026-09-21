import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import './App.css'
import { AuthProvider } from './context/AuthContext'
import AppShell from './layouts/AppShell'
import HomePage from './pages/HomePage'
import LoginPage from './pages/LoginPage'
import RoleDashboardPage from './pages/RoleDashboardPage'
import TravelAlertsPage from './pages/TravelAlertsPage'
import UnauthorizedPage from './pages/UnauthorizedPage'
import ProtectedRoute from './routes/ProtectedRoute'

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/unauthorized" element={<UnauthorizedPage />} />
          <Route element={<ProtectedRoute />}>
            <Route element={<AppShell />}>
              <Route path="/" element={<HomePage />} />
              <Route element={<ProtectedRoute allowedRoles={['TourismProvider']} />}>
                <Route path="/provider" element={<RoleDashboardPage title="Tourism Provider Dashboard" description="A shared workspace for tourism providers." />} />
              </Route>
              <Route element={<ProtectedRoute allowedRoles={['TravelCoordinator']} />}>
                <Route path="/coordinator" element={<RoleDashboardPage title="Travel Coordinator Dashboard" description="A shared workspace for travel coordinators." />} />
              </Route>
              <Route element={<ProtectedRoute allowedRoles={['Administrator']} />}>
                <Route path="/administrator" element={<RoleDashboardPage title="Administrator Dashboard" description="A shared workspace for administrators." />} />
              </Route>
              <Route element={<ProtectedRoute allowedRoles={['TravelCoordinator', 'Administrator']} />}>
                <Route path="/travel-alerts" element={<TravelAlertsPage />} />
              </Route>
            </Route>
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}

export default App
