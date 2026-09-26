enum AgentWorkflowStatus { pending, running, awaitingApproval, completed, failedSafe, cancelled, unknown }

class AgentWorkflowStage {
  const AgentWorkflowStage({required this.sequence, required this.agentRole, required this.status, required this.summary});
  final int sequence;
  final String agentRole;
  final String status;
  final String summary;

  factory AgentWorkflowStage.fromJson(Map<String, dynamic> json) => AgentWorkflowStage(
    sequence: json['sequence'] as int? ?? 0,
    agentRole: json['agentRole']?.toString() ?? 'Unknown',
    status: json['status']?.toString() ?? 'Unknown',
    summary: json['summary'] as String? ?? '',
  );
}

class AgentWorkflow {
  const AgentWorkflow({required this.workflowId, required this.tripId, required this.status, required this.requiresApproval, required this.reviewStatus, required this.executionSucceeded, required this.bookingId, required this.safeMessage, required this.stages});
  final String workflowId;
  final String tripId;
  final AgentWorkflowStatus status;
  final bool requiresApproval;
  final String? reviewStatus;
  final bool? executionSucceeded;
  final String? bookingId;
  final String safeMessage;
  final List<AgentWorkflowStage> stages;

  factory AgentWorkflow.fromJson(Map<String, dynamic> json) => AgentWorkflow(
    workflowId: json['workflowId']?.toString() ?? '',
    tripId: json['tripId']?.toString() ?? '',
    status: _workflowStatus(json['status']),
    requiresApproval: json['requiresApproval'] as bool? ?? false,
    reviewStatus: json['reviewStatus']?.toString(),
    executionSucceeded: json['executionSucceeded'] as bool?,
    bookingId: json['bookingId']?.toString(),
    safeMessage: json['safeMessage'] as String? ?? '',
    stages: (json['stages'] is List ? (json['stages'] as List).whereType<Map<String, dynamic>>().map(AgentWorkflowStage.fromJson).toList() : <AgentWorkflowStage>[]),
  );
}

AgentWorkflowStatus _workflowStatus(Object? value) {
  final normalized = value?.toString().replaceAll('_', '').replaceAll('-', '').toLowerCase();
  return switch (normalized) {
    'pending' => AgentWorkflowStatus.pending,
    'running' => AgentWorkflowStatus.running,
    'awaitingapproval' => AgentWorkflowStatus.awaitingApproval,
    'completed' => AgentWorkflowStatus.completed,
    'failedsafe' => AgentWorkflowStatus.failedSafe,
    'cancelled' => AgentWorkflowStatus.cancelled,
    _ => AgentWorkflowStatus.unknown,
  };
}
