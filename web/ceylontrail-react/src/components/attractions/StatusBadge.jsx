const labels = {
  PendingApproval: 'Pending Approval',
  Approved: 'Approved',
  Rejected: 'Rejected',
}

export default function StatusBadge({ status, isActive = true }) {
  const label = isActive ? (labels[status] || status || 'Unknown') : 'Inactive'
  const tone = isActive ? status?.toLowerCase().replace(/[^a-z]/g, '-') : 'inactive'
  return <span className={`status-badge status-${tone}`}>{label}</span>
}
