const labels = {
  PendingApproval: 'Pending Approval',
  Approved: 'Approved',
  Rejected: 'Rejected',
  UnderReview: 'Under Review',
}

export default function StatusBadge({ status }) {
  const label = labels[status] || status || 'Unknown'
  const tone = (status || 'unknown').toLowerCase().replace(/[^a-z]/g, '-')
  return <span className={`status-badge status-${tone}`}>{label}</span>
}
