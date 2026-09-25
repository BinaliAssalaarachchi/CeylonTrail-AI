import { Link } from 'react-router-dom'

export default function AdminDashboardPage() {
  return <section className="page-section admin-dashboard-page"><div className="consistent-page-header"><p className="eyebrow">Administrator workspace</p><h1>Attraction administration</h1><p className="lead">Review provider-submitted attractions before they become publicly available.</p></div><div className="admin-review-card"><div><p className="eyebrow">Approval workflow</p><h2>Pending attractions</h2><p>Inspect submitted details, schedules, and pricing before approving an attraction for discovery.</p></div><Link className="button button-primary" to="/admin/attractions">Review pending attractions</Link></div></section>
}
