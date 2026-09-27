import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getAdminAttractions } from '../../api/attractions'
import OperationalAnalytics from '../../components/OperationalAnalytics'

export default function AdminDashboardPage() {
  const [stats, setStats] = useState({ total: 0, pending: 0, approved: 0, rejected: 0, underReview: 0 })
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    async function loadStats() {
      try {
        const result = await getAdminAttractions({ page: 1, pageSize: 100 })
        const items = result?.items || []
        setStats({
          total: items.length,
          pending: items.filter((a) => a.status === 'PendingApproval').length,
          approved: items.filter((a) => a.status === 'Approved').length,
          rejected: items.filter((a) => a.status === 'Rejected').length,
          underReview: items.filter((a) => a.status === 'UnderReview').length,
        })
      } catch (e) {
        console.error('Failed to load admin attraction stats', e)
      } finally {
        setLoading(false)
      }
    }
    loadStats()
  }, [])

  return (
    <section className="page-section wide-page admin-dashboard-page">
      <div className="consistent-page-header">
        <p className="eyebrow">Administrator workspace</p>
        <h1>Attraction Administration & Review</h1>
        <p className="lead">
          Manage, approve, reject, or request review on provider attractions across CeylonTrail.
        </p>
      </div>

      <div className="admin-stats-grid" aria-label="Attraction Metrics">
        <Link to="/admin/attractions" className="admin-stat-card">
          <span className="admin-stat-label">Total Attractions</span>
          <strong className="admin-stat-value">{loading ? '…' : stats.total}</strong>
          <span className="admin-stat-hint">In database</span>
        </Link>
        <Link to="/admin/attractions?status=PendingApproval" className="admin-stat-card admin-stat-pending">
          <span className="admin-stat-label">Pending Approval</span>
          <strong className="admin-stat-value">{loading ? '…' : stats.pending}</strong>
          <span className="admin-stat-hint">Requires review</span>
        </Link>
        <Link to="/admin/attractions?status=Approved" className="admin-stat-card admin-stat-approved">
          <span className="admin-stat-label">Approved</span>
          <strong className="admin-stat-value">{loading ? '…' : stats.approved}</strong>
          <span className="admin-stat-hint">Publicly discoverable</span>
        </Link>
        <Link to="/admin/attractions?status=Rejected" className="admin-stat-card admin-stat-rejected">
          <span className="admin-stat-label">Rejected</span>
          <strong className="admin-stat-value">{loading ? '…' : stats.rejected}</strong>
          <span className="admin-stat-hint">Provider notified</span>
        </Link>
      </div>

      <div className="admin-review-card">
        <div>
          <p className="eyebrow">Workflow management</p>
          <h2>Attractions Directory & Approval Queue</h2>
          <p>
            Filter by status (Approved, Rejected, Pending Approval), inspect submitted schedules, pricing, and leave feedback for providers.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
          <Link className="button button-primary" to="/admin/attractions">
            Manage All Attractions
          </Link>
          <Link className="button button-secondary-light" to="/admin/attractions?status=PendingApproval">
            Review Pending Queue ({stats.pending})
          </Link>
        </div>
      </div>
      <OperationalAnalytics />
    </section>
  )
}
