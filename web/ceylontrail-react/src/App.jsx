import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import './App.css'
import { AuthProvider } from './context/AuthContext'
import AppShell from './layouts/AppShell'
import HomePage from './pages/HomePage'
import LoginPage from './pages/LoginPage'
import RoleDashboardPage from './pages/RoleDashboardPage'
import TripPlanningOverviewPage from './pages/TripPlanningOverviewPage'
import ItineraryReviewPage from './pages/ItineraryReviewPage'
import TravelAlertsPage from './pages/TravelAlertsPage'
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
              <Route path="/bookings" element={<RoleDashboardPage title="Reservation & Booking Operations" description="Manage incoming reservations, accept or reject requests, and track status histories." />} />
              <Route element={<ProtectedRoute allowedRoles={['TourismProvider']} />}>
                <Route path="/provider" element={<RoleDashboardPage title="Tourism Provider Dashboard" description="A shared workspace for tourism providers." />} />
                <Route path="/provider/attractions" element={<MyAttractionsPage />} />
                <Route path="/provider/attractions/create" element={<CreateAttractionPage />} />
                <Route path="/provider/attractions/:id/edit" element={<EditAttractionPage />} />
                <Route path="/provider/attractions/:id/schedules" element={<AttractionSchedulesPage />} />
                <Route path="/provider/attractions/:id/slots" element={<ExperienceSlotsPage />} />
                <Route path="/provider/attractions/:id/availability" element={<AttractionAvailabilityPage />} />
              </Route>
              <Route element={<ProtectedRoute allowedRoles={['TravelCoordinator']} />}>
                <Route path="/coordinator" element={<RoleDashboardPage title="Travel Coordinator Dashboard" description="A shared workspace for travel coordinators." />} />
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
              <Route element={<ProtectedRoute allowedRoles={['TravelCoordinator', 'Administrator']} />}>
                <Route path="/trip-planning" element={<TripPlanningOverviewPage />} />
                <Route path="/trip-planning/:id" element={<ItineraryReviewPage />} />
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
