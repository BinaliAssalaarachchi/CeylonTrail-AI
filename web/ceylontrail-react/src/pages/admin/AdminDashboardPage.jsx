import { Link } from 'react-router-dom'

export default function AdminDashboardPage() {
  return <section className="page-section"><p className="eyebrow">Administration</p><h1>Administrator dashboard</h1><p className="lead">Review and approve attraction submissions from tourism providers.</p><div className="notice"><h2>Attraction approvals</h2><p>Pending attractions are reviewed before they become publicly discoverable.</p><Link className="button button-primary" to="/admin/attractions">Review pending attractions</Link></div></section>
}
