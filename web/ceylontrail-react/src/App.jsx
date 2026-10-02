import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import './App.css'
import './premium.css'
import { AuthProvider } from './context/AuthContext'
import AppShell from './layouts/AppShell'
import HomePage from './pages/HomePage'
import LoginPage from './pages/LoginPage'
import RoleDashboardPage from './pages/RoleDashboardPage'
import TripPlanningOverviewPage from './pages/TripPlanningOverviewPage'
import ItineraryReviewPage from './pages/ItineraryReviewPage'
import TravelAlertsPage from './pages/TravelAlertsPage'
import TravelAdvisoriesPage from './pages/TravelAdvisoriesPage'
import AIOperationsPage from './pages/AIOperationsPage'
import UnauthorizedPage from './pages/UnauthorizedPage'
import ProtectedRoute from './routes/ProtectedRoute'
import MyAttractionsPage from './pages/attractions/MyAttractionsPage'
import CreateAttractionPage from './pages/attractions/CreateAttractionPage'
import EditAttractionPage from './pages/attractions/EditAttractionPage'
import AttractionSchedulesPage from './pages/attractions/AttractionSchedulesPage'
import ExperienceSlotsPage from './pages/attractions/ExperienceSlotsPage'
import AttractionAvailabilityPage from './pages/attractions/AttractionAvailabilityPage'
import AdminDashboardPage from './pages/admin/AdminDashboardPage'
import PendingAttractionsPage from './pages/admin/PendingAttractionsPage'
import AdminAttractionDetailsPage from './pages/admin/AdminAttractionDetailsPage'

import DiscoverPage from './pages/DiscoverPage'
import TouristAccessPage from './pages/TouristAccessPage'
import BookingsPage from './pages/BookingsPage'

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/unauthorized" element={<UnauthorizedPage />} />
          <Route element={<ProtectedRoute allowedRoles={['Tourist']} />}>
            <Route path="/tourist-access" element={<TouristAccessPage />} />
          </Route>
          <Route element={<ProtectedRoute />}>
            <Route element={<AppShell />}>
              <Route path="/" element={<HomePage />} />
              <Route element={<ProtectedRoute allowedRoles={['TourismProvider', 'TravelCoordinator', 'Administrator']} />}>
                <Route path="/discover" element={<DiscoverPage />} />
              </Route>
              <Route element={<ProtectedRoute allowedRoles={['TourismProvider', 'TravelCoordinator', 'Administrator']} />}>
                <Route path="/bookings" element={<BookingsPage />} />
              </Route>
              <Route element={<ProtectedRoute allowedRoles={['TravelCoordinator', 'Administrator']} />}>
                <Route path="/trip-planning" element={<TripPlanningOverviewPage />} />
                <Route path="/trip-planning/:id" element={<ItineraryReviewPage />} />
              </Route>
              <Route element={<ProtectedRoute allowedRoles={['TourismProvider']} />}>
                <Route path="/provider" element={<RoleDashboardPage title="Tourism Provider Dashboard" description="Manage your experiences and keep reservations running smoothly." />} />
                <Route path="/provider/attractions" element={<MyAttractionsPage />} />
                <Route path="/provider/attractions/create" element={<CreateAttractionPage />} />
                <Route path="/provider/attractions/:id/edit" element={<EditAttractionPage />} />
                <Route path="/provider/attractions/:id/schedules" element={<AttractionSchedulesPage />} />
                <Route path="/provider/attractions/:id/slots" element={<ExperienceSlotsPage />} />
                <Route path="/provider/attractions/:id/availability" element={<AttractionAvailabilityPage />} />
              </Route>
              <Route element={<ProtectedRoute allowedRoles={['TravelCoordinator']} />}>
                <Route path="/coordinator" element={<RoleDashboardPage />} />
              </Route>
              <Route element={<ProtectedRoute allowedRoles={['Administrator']} />}>
                <Route path="/administrator" element={<AdminDashboardPage />} />
                <Route path="/admin/attractions" element={<PendingAttractionsPage />} />
                <Route path="/admin/attractions/:id" element={<AdminAttractionDetailsPage />} />
              </Route>
              <Route element={<ProtectedRoute allowedRoles={['TravelCoordinator', 'Administrator']} />}>
                <Route path="/travel-alerts" element={<TravelAlertsPage />} />
                <Route path="/ai-operations" element={<AIOperationsPage />} />
              </Route>
              <Route element={<ProtectedRoute allowedRoles={['Tourist', 'TourismProvider']} />}>
                <Route path="/travel-advisories" element={<TravelAdvisoriesPage />} />
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
