import 'package:flutter_test/flutter_test.dart';

import 'package:ceylontrail_flutter/models/agent_workflow_model.dart';

void main() {
  test('parses safe workflow status and four ordered stages', () {
    final workflow = AgentWorkflow.fromJson({
      'workflowId': 'workflow-1',
      'tripId': 'trip-1',
      'status': 'AwaitingApproval',
      'requiresApproval': true,
      'reviewStatus': 'Pending',
      'executionSucceeded': null,
      'stages': [
        {'sequence': 1, 'agentRole': 'Planner', 'status': 'Completed', 'summary': 'Planner complete.'},
        {'sequence': 2, 'agentRole': 'Destination', 'status': 'Completed', 'summary': 'Destination complete.'},
        {'sequence': 3, 'agentRole': 'BookingAction', 'status': 'Completed', 'summary': 'Proposal ready.'},
        {'sequence': 4, 'agentRole': 'TravelIntelligence', 'status': 'Completed', 'summary': 'Review pending.'},
      ],
    });

    expect(workflow.status, AgentWorkflowStatus.awaitingApproval);
    expect(workflow.requiresApproval, isTrue);
    expect(workflow.stages.map((stage) => stage.agentRole), ['Planner', 'Destination', 'BookingAction', 'TravelIntelligence']);
  });

  test('maps safe execution result and unknown status', () {
    final workflow = AgentWorkflow.fromJson({
      'workflowId': 'workflow-2',
      'tripId': 'trip-2',
      'status': 'FutureStatus',
      'executionSucceeded': false,
      'safeMessage': 'Booking failed safely.',
      'bookingId': null,
    });

    expect(workflow.status, AgentWorkflowStatus.unknown);
    expect(workflow.executionSucceeded, isFalse);
    expect(workflow.safeMessage, 'Booking failed safely.');
    expect(workflow.stages, isEmpty);
  });
}
