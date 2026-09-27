import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import { approveApprovalRequest, getApprovalRequest, rejectApprovalRequest } from '../api/approvals'
import { getAgentWorkflow, getTravelIntelligenceExecution, getTravelIntelligenceExecutions } from '../api/travelIntelligenceExecutions'
import { AuthContext } from '../context/AuthContext'

const dateFormatter = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })
const actionLabels = { Proceed: 'Proceed with trip', ProceedWithCaution: 'Proceed with caution', Reschedule: 'Reschedule trip', Reroute: 'Reroute trip', ReviewBudget: 'Review trip budget', ResolveScheduleConflict: 'Resolve schedule conflict', ManualReview: 'Review trip manually' }

function formatDate(value) {
  if (!value) return 'Not available'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? 'Not available' : dateFormatter.format(date)
}
function getErrorMessage(error, fallback) { return error.response?.data?.message || fallback }
function formatAction(value) { return actionLabels[value] || value?.replace(/([a-z])([A-Z])/g, '$1 $2') || 'Review trip' }
function formatLabel(value) { return value?.replace(/([a-z])([A-Z])/g, '$1 $2') || 'Not available' }
function statusClass(value) { return 'status-badge status-' + (value?.toLowerCase() || 'pending') }
function approvalStatus(execution) { return execution.approval?.status || (execution.requiresHumanApproval ? 'Awaiting link' : 'Not required') }
function fallbackMessage(execution) {
  if (execution.usedFallback && execution.providerAttempted && !execution.providerSucceeded) return 'External AI was unavailable, so CeylonTrail completed the safety assessment using its deterministic validation rules.'
  if (execution.usedFallback) return 'CeylonTrail used its safe deterministic fallback for this assessment.'
  if (execution.providerSucceeded) return 'External AI analysis was used; deterministic safety rules remained authoritative.'
  return 'Provider activity was not available for this execution.'
}

function Fact({ label, children }) { return <span><b>{label}</b>{children || 'Not available'}</span> }
function EmptyBlock({ children = 'Not available for this execution.' }) { return <p className="ai-empty-inline">{children}</p> }
function ListSection({ title, children, empty }) { return <section className="ai-detail-section"><p className="eyebrow">{title}</p>{children || <EmptyBlock>{empty}</EmptyBlock>}</section> }

export default function AIOperationsPage() {
  const { user } = useContext(AuthContext)
  const canDecide = ['TravelCoordinator', 'Administrator'].includes(user?.role)
  const [history, setHistory] = useState({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
  const [selectedId, setSelectedId] = useState('')
  const [selected, setSelected] = useState(null)
  const [workflow, setWorkflow] = useState(null)
  const [page, setPage] = useState(1)
  const [executionStatus, setExecutionStatus] = useState('')
  const [usedFallback, setUsedFallback] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isDetailLoading, setIsDetailLoading] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [feedback, setFeedback] = useState('')
  const [comment, setComment] = useState('')
  const selectedIdRef = useRef(selectedId)

  const loadHistory = useCallback(async (nextPage = 1, preserveSelection = true) => {
    setIsLoading(true); setError('')
    try {
      const response = await getTravelIntelligenceExecutions({ page: nextPage, pageSize: 20, executionStatus, usedFallback: usedFallback === '' ? undefined : usedFallback === 'true' })
      setHistory(response); setPage(response.page)
      const currentSelectedId = selectedIdRef.current
      const nextSelected = preserveSelection && response.items.some((item) => item.executionId === currentSelectedId) ? currentSelectedId : response.items[0]?.executionId || ''
      setSelectedId(nextSelected); if (!nextSelected) setSelected(null)
    } catch (requestError) {
      setHistory({ items: [], page: nextPage, pageSize: 20, totalCount: 0, totalPages: 0 }); setSelectedId(''); setSelected(null)
      setError(getErrorMessage(requestError, 'Unable to load AI execution history.'))
    } finally { setIsLoading(false) }
  }, [executionStatus, usedFallback])

  useEffect(() => { selectedIdRef.current = selectedId }, [selectedId])
  useEffect(() => { loadHistory(1, false) }, [loadHistory])
  useEffect(() => {
    if (!selectedId) return undefined
    let current = true; setIsDetailLoading(true)
    getTravelIntelligenceExecution(selectedId)
      .then(async (execution) => {
        if (!current) return
        const approval = execution.approval?.id ? await getApprovalRequest(execution.approval.id) : execution.approval
        const enrichedExecution = approval ? { ...execution, approval } : execution
        setSelected(enrichedExecution)
        setWorkflow(approval?.agentWorkflowId ? await getAgentWorkflow(approval.agentWorkflowId) : null)
      })
      .catch((requestError) => { if (current) setError(getErrorMessage(requestError, 'Unable to load execution details.')) })
      .finally(() => { if (current) setIsDetailLoading(false) })
    return () => { current = false }
  }, [selectedId])

  const pendingCount = useMemo(() => history.items.filter((item) => item.approval?.status === 'Pending').length, [history.items])
  async function decide(decision) {
    const approval = selected?.approval
    if (!canDecide || !approval || approval.status !== 'Pending') return
    setIsSubmitting(true); setError(''); setFeedback('')
    try {
      if (decision === 'Approved') await approveApprovalRequest(approval.id, comment)
      else await rejectApprovalRequest(approval.id, comment)
      setComment(''); setFeedback('Recommendation ' + decision.toLowerCase() + ' successfully.')
      await Promise.all([loadHistory(page), getTravelIntelligenceExecution(selectedId).then(async (execution) => { const approval = execution.approval?.id ? await getApprovalRequest(execution.approval.id) : execution.approval; const enrichedExecution = approval ? { ...execution, approval } : execution; setSelected(enrichedExecution); setWorkflow(approval?.agentWorkflowId ? await getAgentWorkflow(approval.agentWorkflowId) : null) })])
    } catch (requestError) { setError(getErrorMessage(requestError, 'Unable to update this recommendation.')) }
    finally { setIsSubmitting(false) }
  }

  return <section className="ai-operations-page" aria-labelledby="ai-operations-title">
    <div className="page-header"><div><p className="eyebrow">Human-in-the-loop intelligence</p><h1 id="ai-operations-title">AI Recommendations</h1><p className="lead">Review persisted safety assessments, investigation checks, and human-review decisions before any high-impact action is taken.</p><p className="ai-operations-process">Problem detected → investigation → recommendation → human review → decision.</p></div><div className="ai-operations-count"><strong>{pendingCount}</strong><span>pending on this page</span></div></div>
    {feedback && <p className="inline-notice" role="status">{feedback}<button type="button" onClick={() => setFeedback('')} aria-label="Dismiss message">×</button></p>}
    {error && <div className="form-feedback ai-operations-feedback" role="alert">{error}<button className="button button-secondary" type="button" onClick={() => loadHistory(page)}>Retry</button></div>}
    <div className="ai-operations-toolbar"><div><p className="eyebrow">EXECUTION HISTORY</p><h2>Travel intelligence runs</h2><p className="ai-toolbar-count">{history.totalCount} persisted execution{history.totalCount === 1 ? '' : 's'}</p></div><div className="ai-filter-row"><label className="filter-field">Execution status<select value={executionStatus} onChange={(event) => setExecutionStatus(event.target.value)}><option value="">All statuses</option><option value="Completed">Completed</option><option value="Fallback">Fallback</option></select></label><label className="filter-field">Provider outcome<select value={usedFallback} onChange={(event) => setUsedFallback(event.target.value)}><option value="">All outcomes</option><option value="false">Provider analysis</option><option value="true">Fallback used</option></select></label></div></div>
    <div className="ai-operations-grid"><div className="approval-list-panel">
      {isLoading && <div className="state-message" role="status">Loading execution history…</div>}
      {!isLoading && !error && history.items.length === 0 && <div className="state-message"><strong>No persisted executions found.</strong><span>Completed travel-intelligence assessments will appear here.</span></div>}
      {!isLoading && history.items.length > 0 && <div className="approval-list">{history.items.map((execution) => <button className={'approval-list-item ' + (execution.executionId === selectedId ? 'selected' : '')} key={execution.executionId} type="button" onClick={() => setSelectedId(execution.executionId)}><span className="approval-list-item-top"><strong>{formatAction(execution.recommendedAction)}</strong><span className={statusClass(approvalStatus(execution))}>{approvalStatus(execution)}</span></span><span className="approval-list-summary">{execution.summary || 'No summary available.'}</span><span className="ai-list-evidence"><span className={'ai-risk risk-' + execution.riskLevel?.toLowerCase()}>{execution.riskLevel} risk</span><span>{execution.usedFallback ? 'Fallback' : execution.provider || 'Provider unavailable'}</span></span><span className="approval-list-meta">{formatDate(execution.startedAt)} · {formatLabel(execution.executionStatus)}</span></button>)}</div>}
      {!isLoading && history.totalPages > 1 && <div className="ai-pagination"><button type="button" disabled={page <= 1} onClick={() => loadHistory(page - 1, false)}>Previous</button><span>Page {page} of {history.totalPages}</span><button type="button" disabled={page >= history.totalPages} onClick={() => loadHistory(page + 1, false)}>Next</button></div>}
    </div><div className="approval-detail-panel">
      {isDetailLoading && <div className="state-message" role="status">Loading execution details…</div>}
      {!isDetailLoading && !selected && <div className="state-message">Select an execution to review its evidence.</div>}
      {!isDetailLoading && selected && <><ExecutionDetail execution={canDecide && selected.approval?.status === 'Pending' ? { ...selected, requiresHumanApproval: true } : selected} comment={comment} setComment={setComment} isSubmitting={isSubmitting} decide={decide} /><WorkflowReview workflow={workflow} /></>}
    </div></div>
  </section>
}

function WorkflowReview({ workflow }) {
  if (!workflow) return null
  return <section className="ai-detail-section"><p className="eyebrow">FOUR-AGENT WORKFLOW</p><div className="ai-step-list">
    <div className="approval-facts"><Fact label="Trip">{workflow.tripId}</Fact><Fact label="Workflow">{workflow.workflowId}</Fact><Fact label="Overall status">{formatLabel(workflow.status)}</Fact><Fact label="Execution">{workflow.executionSucceeded === true ? `Succeeded${workflow.bookingId ? ` · Booking ${workflow.bookingId}` : ''}` : workflow.executionSucceeded === false ? 'Failed safely' : 'Not executed'}</Fact></div>
    {(workflow.stages || []).map((stage) => <div className="ai-step" key={stage.sequence}><div className="ai-step-number">{stage.sequence}</div><div><div className="ai-step-heading"><strong>{formatLabel(stage.agentRole)}</strong><span className={statusClass(stage.status)}>{formatLabel(stage.status)}</span></div><p>{stage.summary || 'No stage summary available.'}</p></div></div>)}
    {workflow.bookingProposals?.length > 0 && <div className="ai-item-grid">{workflow.bookingProposals.map((proposal) => <div className="ai-item-card" key={proposal.availabilitySlotId}><strong>Booking proposal</strong><span>Attraction {proposal.attractionId}</span><span>Slot {proposal.availabilitySlotId}</span><small>{proposal.guestCount} guest(s) · LKR {proposal.proposedUnitPrice} · {formatDate(proposal.startTime)}</small></div>)}</div>}
    {workflow.executionMessage && <p className="non-actionable-notice">{workflow.executionMessage}</p>}
  </div></section>
}

function ExecutionDetail({ execution, comment, setComment, isSubmitting, decide }) {
  const approval = execution.approval
  return <article className="approval-detail"><div className="approval-detail-heading"><div><p className="eyebrow">Execution review</p><h2>{formatAction(execution.recommendedAction)}</h2><p className="ai-execution-id">Execution {execution.executionId}</p></div><span className={statusClass(approvalStatus(execution))}>{approvalStatus(execution)}</span></div>
    <div className="approval-facts"><Fact label="Risk level">{execution.riskLevel}</Fact><Fact label="Feasibility">{execution.isFeasible ? 'Feasible' : 'Not feasible'}</Fact><Fact label="Execution">{formatLabel(execution.executionStatus)}</Fact><Fact label="Started">{formatDate(execution.startedAt)}</Fact><Fact label="Duration">{execution.durationMs ? execution.durationMs + ' ms' : 'Not available'}</Fact></div>
    <ListSection title="PROBLEM DETECTED"><p className="approval-summary">{execution.summary || 'No summary available.'}</p></ListSection>
    <ListSection title="AI OBJECTIVE"><div className="ai-objective-card"><h3>{execution.objectiveName || 'Objective not available'}</h3><p>{execution.objectiveDescription || 'No objective description available.'}</p><span>{execution.agentName || 'Agent not available'} · version {execution.agentVersion || 'Not available'} · source {execution.objectiveSource || 'Not available'}</span></div></ListSection>
    <ListSection title="INVESTIGATION / CHECKS PERFORMED" empty="No investigation steps were recorded."><div className="ai-step-list">{(execution.steps || []).map((step) => <div className="ai-step" key={step.sequence + '-' + step.stepId}><div className="ai-step-number">{step.sequence}</div><div><div className="ai-step-heading"><strong>{step.name || step.stepId}</strong><span className={statusClass(step.status)}>{formatLabel(step.status)}</span></div><p>{step.purpose || 'No purpose recorded.'}</p><small>Check/tool: {step.executedToolName || step.plannedToolName || 'Not available'}{step.durationMs ? ' · ' + step.durationMs + ' ms' : ''}</small>{step.resultSummary && <div className="ai-step-result">{step.resultSummary}</div>}</div></div>)}</div></ListSection>
    <ListSection title="AFFECTED TRIP ITEMS" empty="No affected itinerary items were recorded."><div className="ai-item-grid">{(execution.affectedItems || []).map((item) => <div className="ai-item-card" key={item.itemReference}><strong>{item.title || item.itemReference}</strong><span>{item.district || 'District not available'}{item.isBlocking ? ' · Blocking' : ''}</span><small>{item.issueTypes?.map(formatLabel).join(', ') || 'Issue details not available'}</small></div>)}</div></ListSection>
    <ListSection title="AI RECOMMENDATION"><div className="ai-recommendation-card"><h3>{formatAction(execution.recommendedAction)}</h3><p>{execution.resultSummary || execution.summary || 'Recommendation details are not available.'}</p><span>{execution.requiresHumanApproval ? 'Human approval required before high-impact action.' : 'Human approval not required for this execution.'}</span></div>{execution.recommendations?.length > 0 && <div className="ai-recommendation-list">{execution.recommendations.map((recommendation, index) => <div key={index}><strong>{formatAction(recommendation.action)}</strong><p>{recommendation.explanation}</p></div>)}</div>}</ListSection>
    <ListSection title="SUGGESTED ALTERNATIVES" empty="No alternatives were recorded."><div className="ai-item-grid">{(execution.alternatives || []).map((alternative) => <div className="ai-item-card" key={alternative.alternativeId}><strong>{formatAction(alternative.action)}</strong><span>{alternative.rationale}</span><small>{alternative.safetyStatus ? formatLabel(alternative.safetyStatus) : 'Safety status not available'}{alternative.requiresHumanApproval ? ' · Approval required' : ''}</small></div>)}</div></ListSection>
    <ListSection title="SAFER TIME WINDOWS" empty="No safe-window suggestions were recorded."><div className="ai-item-grid">{(execution.safeWindows || []).map((window, index) => <div className="ai-item-card" key={window.itemReference + '-' + index}><strong>{window.itemReference}</strong><span>{formatDate(window.proposedStart)} – {formatDate(window.proposedEnd)}</span><small>{window.reason || 'No reason recorded.'} {window.constraints?.length ? '· ' + window.constraints.join(', ') : ''}</small></div>)}</div></ListSection>
    <ListSection title="AI EXECUTION EVIDENCE"><div className="ai-evidence-card"><strong>{fallbackMessage(execution)}</strong><div className="approval-facts"><Fact label="Provider">{execution.providerName || execution.provider}</Fact><Fact label="Model">{execution.modelName}</Fact><Fact label="Attempts">{execution.providerAttemptCount ?? 'Not available'}</Fact><Fact label="Latency">{execution.providerLatencyMs ? execution.providerLatencyMs + ' ms' : 'Not available'}</Fact></div>{execution.selectedToolNames?.length > 0 && <p><b>Selected checks:</b> {execution.selectedToolNames.join(', ')}</p>}{execution.rejectedToolNames?.length > 0 && <p><b>Rejected checks:</b> {execution.rejectedToolNames.join(', ')}</p>}</div></ListSection>
    <ListSection title="HUMAN REVIEW"><div className="ai-review-card">{approval ? <><div className="approval-facts"><Fact label="Approval status">{approval.status}</Fact><Fact label="Requested">{formatDate(approval.createdAt)}</Fact><Fact label="Decision">{approval.decision ? formatLabel(approval.decision.decision) : 'Not decided'}</Fact><Fact label="Decided">{formatDate(approval.decision?.decidedAt)}</Fact></div>{approval.decision?.comment && <p className="ai-review-comment">“{approval.decision.comment}”</p>}</> : <EmptyBlock>{execution.requiresHumanApproval ? 'Approval is required, but no linked approval request is available.' : 'No human approval is required for this execution.'}</EmptyBlock>}{approval?.status === 'Pending' && execution.requiresHumanApproval && <div className="approval-actions"><label className="form-field form-field-wide">Reviewer comment <span className="field-optional">(optional)</span><textarea value={comment} onChange={(event) => setComment(event.target.value)} maxLength="1000" rows="3" placeholder="Add context for the review history…" /></label><div className="modal-actions"><button className="button button-danger" type="button" disabled={isSubmitting} onClick={() => decide('Rejected')}>{isSubmitting ? 'Saving…' : 'Reject recommendation'}</button><button className="button button-primary" type="button" disabled={isSubmitting} onClick={() => decide('Approved')}>{isSubmitting ? 'Saving…' : 'Approve recommendation'}</button></div></div>}{approval && approval.status !== 'Pending' && <p className="non-actionable-notice">This recommendation has already been reviewed. No further action is required.</p>}</div></ListSection>
  </article>
}
