export default function RoleDashboardPage({ title, description }) {
  return (
    <section className="workspace-page" aria-labelledby="workspace-title">
      <div className="page-heading">
        <p className="eyebrow">Role workspace</p>
        <h1 id="workspace-title">{title}</h1>
        <p className="lead">{description}</p>
      </div>
      <div className="workspace-placeholder">
        <div className="placeholder-mark" aria-hidden="true">✦</div>
        <div>
          <p className="eyebrow">Ready for the next layer</p>
          <h2>Your team workspace is prepared.</h2>
          <p className="muted">Member-specific tools will be added here in their own feature phases. The shared navigation, identity and responsive layout are already available.</p>
        </div>
      </div>
    </section>
  )
}
