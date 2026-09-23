import { useEffect, useMemo, useState } from 'react'
import { approveApprovalRequest, getApprovalRequest, getApprovalRequests, rejectApprovalRequest } from '../api/approvals'

const statuses = ['', 'Pending', 'Approved', 'Rejected']
const dateFormatter = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })

function formatDate(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : dateFormatter.format(date)
}

function getErrorMessage(error, fallback = 'The request could not be completed.') {
  return error.response?.data?.message || fallback
}

function statusClass(value) {
  return 'status-badge status-' + (value?.toLowerCase() || 'pending')
}

const actionLabels = {
  Proceed: 'Proceed with trip',
  ProceedWithCaution: 'Proceed with caution',
  Reschedule: 'Reschedule trip',
  Reroute: 'Reroute trip',
  ReviewBudget: 'Review trip budget',
  ResolveScheduleConflict: 'Resolve schedule conflict',
  ManualReview: 'Review trip manually',
}

function formatAction(value) {
  return actionLabels[value] || value?.replace(/([a-z])([A-Z])/g, '$1 $2') || 'Review trip'
}

function formatRecommendationSummary(summary) {
  const validationMatch = summary?.match(
    /^Deterministic validation is (\w+) with (\d+) issue\(s\), including (\d+) blocking issue\(s)\.?$/i,
  )

  if (!validationMatch) return summary

  const [, validationStatus, issueCount, blockingIssueCount] = validationMatch
  const issues = Number(issueCount)
  const blockingIssues = Number(blockingIssueCount)
  const issueText = `${issues} issue${issues === 1 ? '' : 's'} found`
  const blockingText = `${blockingIssues} serious issue${blockingIssues === 1 ? '' : 's'}`

  if (validationStatus.toLowerCase() === 'invalid' || blockingIssues > 0) {
    return `This trip has a serious issue that should be resolved before travel. ${issueText}, including ${blockingText} that require attention.`
  }

  if (validationStatus.toLowerCase() === 'warning') {
    return `This trip has issues that should be reviewed before travel. ${issueText}.`
  }

  return `This trip passed its checks. ${issueText}.`
}

export default function AIOperationsPage() {
  const [status, setStatus] = useState('')
  const [requests, setRequests] = useState([])
  const [selectedId, setSelectedId] = useState('')
  const [selected, setSelected] = useState(null)
  const [comment, setComment] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isDetailLoading, setIsDetailLoading] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [feedback, setFeedback] = useState('')

  async function loadRequests() {
    setIsLoading(true)
    setError('')
    try {
      const nextRequests = await getApprovalRequests(status)
      setRequests(nextRequests)
      setSelectedId((current) => nextRequests.some((request) => request.id === current) ? current : nextRequests[0]?.id || '')
    } catch (requestError) {
      setRequests([])
      setSelectedId('')
      setError(getErrorMessage(requestError, 'Unable to load trip recommendations.'))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    loadRequests()
  }, [status])

  useEffect(() => {
    if (!selectedId) {
      setSelected(null)
      return
    }
    let current = true
    setIsDetailLoading(true)
    getApprovalRequest(selectedId)
      .then((request) => { if (current) setSelected(request) })
      .catch((requestError) => { if (current) setError(getErrorMessage(requestError, 'Unable to load recommendation details.')) })
      .finally(() => { if (current) setIsDetailLoading(false) })
    return () => { current = false }
  }, [selectedId])

  const pendingCount = useMemo(() => requests.filter((request) => request.status === 'Pending').length, [requests])

  async function decide(decision) {
    if (!selected || selected.status !== 'Pending') return
    setIsSubmitting(true)
    setError('')
    setFeedback('')
    try {
      const updated = decision === 'Approved'
        ? await approveApprovalRequest(selected.id, comment)
        : await rejectApprovalRequest(selected.id, comment)
      setSelected(updated)
      setRequests((current) => current.map((request) => request.id === updated.id ? updated : request))
      setComment('')
      setFeedback('Recommendation ' + decision.toLowerCase() + ' successfully.')
    } catch (requestError) {
      setError(getErrorMessage(requestError, 'Unable to update this recommendation.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <section className="ai-operations-page" aria-labelledby="ai-operations-title">
      <div className="page-header">
        <div>
          <p className="eyebrow">Human-in-the-loop intelligence</p>
          <h1 id="ai-operations-title">AI Recommendations</h1>
          <p className="lead">Review AI recommendations for trips with safety, schedule, or budget issues before any action is taken.</p>
          <p className="ai-operations-process">Trip checked → issue found → AI recommendation → coordinator review → approve or reject.</p>
        </div>
        <div className="ai-operations-count">
          <strong>{pendingCount}</strong>
          <span>pending review</span>
        </div>
      </div>

      {feedback && <p className="inline-notice" role="status">{feedback}<button type="button" onClick={() => setFeedback('')} aria-label="Dismiss message">×</button></p>}
      {error && <p className="form-feedback ai-operations-feedback" role="alert">{error}</p>}

      <div className="ai-operations-toolbar">
        <div>
          <p className="eyebrow">REVIEW QUEUE</p>
          <h2>Trip recommendations</h2>
        </div>
        <label className="filter-field ai-status-filter">Status
          <select value={status} onChange={(event) => setStatus(event.target.value)}>
            <option value="">Pending first · All recommendations</option>
            {statuses.slice(1).map((value) => <option key={value} value={value}>{value}</option>)}
          </select>
        </label>
      </div>

      <div className="ai-operations-grid">
        <div className="approval-list-panel">
          {isLoading && <div className="state-message" role="status">Loading trip recommendations…</div>}
          {!isLoading && !error && requests.length === 0 && (
            <div className="state-message">
              <strong>No trip recommendations found.</strong>
              <span>Pending recommendations will appear here for coordinator review.</span>
            </div>
          )}
          {!isLoading && requests.length > 0 && (
            <div className="approval-list">
              {requests.map((request) => (
                <button
                  className={'approval-list-item ' + (request.id === selectedId ? 'selected' : '')}
                  key={request.id}
                  type="button"
                  onClick={() => setSelectedId(request.id)}
                >
                  <span className="approval-list-item-top">
                    <strong>{formatAction(request.recommendedAction)}</strong>
                    <span className={statusClass(request.status)}>{request.status}</span>
                  </span>
                  <span className="approval-list-summary">{formatRecommendationSummary(request.summary)}</span>
                  <span className="approval-list-meta">{request.riskLevel} risk · {formatDate(request.createdAt)}</span>
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="approval-detail-panel">
          {isDetailLoading && <div className="state-message" role="status">Loading recommendation details…</div>}
          {!isDetailLoading && !selected && <div className="state-message">Select a recommendation to review its details.</div>}
          {!isDetailLoading && selected && (
            <article className="approval-detail">
              <div className="approval-detail-heading">
                <div>
                  <p className="eyebrow">Recommendation detail</p>
                  <h2>{formatAction(selected.recommendedAction)}</h2>
                </div>
                <span className={statusClass(selected.status)}>{selected.status}</span>
              </div>
              <div className="approval-facts">
                <span><b>Risk level</b>{selected.riskLevel}</span>
                <span><b>Trip reference</b>{selected.tripReference || '—'}</span>
                <span><b>Created</b>{formatDate(selected.createdAt)}</span>
              </div>
              <p className="approval-summary">{formatRecommendationSummary(selected.summary)}</p>
              {selected.affectedItemReferences?.length > 0 && (
                <div className="approval-items">
                  <p className="eyebrow">AFFECTED TRIP ITEMS</p>
                  <div>{selected.affectedItemReferences.map((reference) => <span key={reference}>{reference}</span>)}</div>
                </div>
              )}
              {selected.decision && (
                <div className="approval-decision">
                  <p className="eyebrow">REVIEW HISTORY</p>
                  <strong>{selected.decision.decision}</strong>
                  <span>{formatDate(selected.decision.decidedAt)}</span>
                  {selected.decision.comment && <p>{selected.decision.comment}</p>}
                </div>
              )}
              {selected.status === 'Pending' ? (
                <div className="approval-actions">
                  <label className="form-field form-field-wide">Reviewer comment <span className="field-optional">(optional)</span>
                    <textarea value={comment} onChange={(event) => setComment(event.target.value)} maxLength="1000" rows="3" placeholder="Add context for the review history…" />
                  </label>
                  <div className="modal-actions">
                    <button className="button button-danger" type="button" disabled={isSubmitting} onClick={() => decide('Rejected')}>{isSubmitting ? 'Saving…' : 'Reject recommendation'}</button>
                    <button className="button button-primary" type="button" disabled={isSubmitting} onClick={() => decide('Approved')}>{isSubmitting ? 'Saving…' : 'Approve recommendation'}</button>
                  </div>
                </div>
              ) : (
                <p className="non-actionable-notice">This recommendation has already been reviewed. No further action is required.</p>
              )}
            </article>
          )}
        </div>
      </div>
    </section>
  )
}
