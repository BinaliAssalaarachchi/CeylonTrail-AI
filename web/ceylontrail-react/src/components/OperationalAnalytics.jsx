import { useEffect, useState } from 'react'
import { getReportOverview } from '../api/reports'
import { useAuth } from '../context/useAuth'

function getErrorMessage(error) {
  return error.response?.data?.message || 'Unable to load operational analytics.'
}

function Breakdown({ title, values }) {
  const entries = Object.entries(values || {})
  return (
    <div className="analytics-breakdown">
      <span className="analytics-breakdown-title">{title}</span>
      {entries.length === 0 ? <span className="muted">No records</span> : entries.map(([label, value]) => (
        <span className="analytics-breakdown-row" key={label}><span>{label}</span><strong>{value}</strong></span>
      ))}
    </div>
  )
}

function Metric({ label, value }) {
  return <article className="admin-stat-card"><span className="admin-stat-label">{label}</span><strong className="admin-stat-value">{value}</strong></article>
}

export default function OperationalAnalytics() {
  const { user } = useAuth()
  const [overview, setOverview] = useState(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)

  const isStaff = ['TravelCoordinator', 'Administrator'].includes(user?.role)

  useEffect(() => {
    if (!isStaff) return undefined
    let current = true
    getReportOverview()
      .then((data) => { if (current) setOverview(data) })
      .catch((requestError) => { if (current) setError(getErrorMessage(requestError)) })
      .finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [isStaff])

  if (!isStaff) return null

  if (loading) return <section className="admin-review-card" aria-label="Operational analytics"><p>Loading operational analytics…</p></section>
  if (error) return <section className="admin-review-card" aria-label="Operational analytics"><p className="form-error notice-error" role="alert">{error}</p></section>

  const data = overview || {}
  return (
    <section className="analytics-section" aria-labelledby="analytics-title">
      <div className="consistent-page-header"><p className="eyebrow">Authoritative platform metrics</p><h2 id="analytics-title">Operational analytics</h2></div>
      <div className="admin-stats-grid">
        <Metric label="Total Trips" value={data.totalTrips ?? 0} />
        <Metric label="Total Bookings" value={data.totalBookings ?? 0} />
        <Metric label="Awaiting Approval" value={data.pendingApprovalRequests ?? 0} />
        <Metric label="Active Travel Alerts" value={data.activeTravelAlerts ?? 0} />
        <Metric label="Completed AI Workflows" value={data.completedWorkflows ?? 0} />
        <Metric label="Failed-Safe Workflows" value={data.failedSafeWorkflows ?? 0} />
      </div>
      <div className="analytics-breakdown-grid">
        <Breakdown title="Trips by status" values={data.tripsByStatus} />
        <Breakdown title="Bookings by status" values={data.bookingsByStatus} />
        <Breakdown title="Approval requests by status" values={data.approvalRequestsByStatus} />
        <Breakdown title="Active alerts by severity" values={data.activeAlertsBySeverity} />
        <Breakdown title="Workflows by status" values={data.workflowsByStatus} />
      </div>
    </section>
  )
}
