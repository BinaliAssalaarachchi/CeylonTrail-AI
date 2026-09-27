import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ceylontrail_flutter/models/agent_workflow_model.dart';
import 'package:ceylontrail_flutter/models/auth_user.dart';
import 'package:ceylontrail_flutter/models/trip_model.dart';
import 'package:ceylontrail_flutter/services/api_client.dart';
import 'package:ceylontrail_flutter/services/auth_storage.dart';
import 'package:ceylontrail_flutter/services/trip_api_service.dart';
import 'package:ceylontrail_flutter/services/travel_intelligence_api_service.dart';
import 'package:ceylontrail_flutter/views/itinerary_page.dart';

class _Storage implements AuthSessionStorage {
  @override
  Future<void> clear() async {}

  @override
  Future<StoredSession?> read() async => null;

  @override
  Future<void> write({required String token, required DateTime expiresAt, required AuthUser user}) async {}
}

class _TripApi extends TripApiService {
  _TripApi({this.itinerary}) : super(ApiClient(storage: _Storage()));

  final Itinerary? itinerary;

  @override
  Future<Itinerary?> getItinerary(String tripId) async => itinerary;

  @override
  Future<List<ItineraryHistoryItem>> getItineraryHistory(String tripId) async => const [];
}

class _WorkflowSource implements AgentWorkflowSource {
  _WorkflowSource(this.workflow);

  final AgentWorkflow? workflow;

  @override
  Future<AgentWorkflow?> fetchWorkflow(String tripId) async => workflow;
}

Itinerary _itinerary() => Itinerary(
      id: 'itinerary-1',
      tripId: 'trip-1',
      status: 'Active',
      totalEstimatedCost: 1000,
      createdAt: DateTime(2026, 9, 27),
      updatedAt: DateTime(2026, 9, 27),
      days: [
        ItineraryDay(
          id: 'day-1',
          dayNumber: 1,
          date: DateTime(2026, 10, 1),
          items: const [],
        ),
      ],
    );

AgentWorkflow _workflow(AgentWorkflowStatus status) => AgentWorkflow(
      workflowId: 'workflow-1',
      tripId: 'trip-1',
      status: status,
      requiresApproval: status == AgentWorkflowStatus.awaitingApproval,
      reviewStatus: status == AgentWorkflowStatus.awaitingApproval ? 'Pending' : null,
      executionSucceeded: null,
      bookingId: null,
      safeMessage: status == AgentWorkflowStatus.failedSafe ? 'Workflow failed safely.' : '',
      stages: const [
        AgentWorkflowStage(sequence: 4, agentRole: 'TravelIntelligence', status: 'Completed', summary: 'Safety complete.'),
        AgentWorkflowStage(sequence: 2, agentRole: 'Destination', status: 'Completed', summary: 'Destination complete.'),
        AgentWorkflowStage(sequence: 1, agentRole: 'Planner', status: 'Completed', summary: 'Planner complete.'),
        AgentWorkflowStage(sequence: 3, agentRole: 'BookingAction', status: 'Completed', summary: 'Booking proposal complete.'),
      ],
    );

void main() {
  testWidgets('displays itinerary and all ordered workflow stages', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: ItineraryPage(
        api: _TripApi(itinerary: _itinerary()),
        tripId: 'trip-1',
        workflowSource: _WorkflowSource(_workflow(AgentWorkflowStatus.completed)),
      ),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Itinerary'), findsOneWidget);
    expect(find.text('Status: Completed'), findsOneWidget);
    expect(find.textContaining('Planner:'), findsOneWidget);
    expect(find.textContaining('Destination:'), findsOneWidget);
    expect(find.textContaining('BookingAction:'), findsOneWidget);
    expect(find.textContaining('TravelIntelligence:'), findsOneWidget);
  });

  testWidgets('displays awaiting approval workflow state', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: ItineraryPage(
        api: _TripApi(itinerary: _itinerary()),
        tripId: 'trip-1',
        workflowSource: _WorkflowSource(_workflow(AgentWorkflowStatus.awaitingApproval)),
      ),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Status: Awaiting approval'), findsOneWidget);
    expect(find.text('Approval required'), findsOneWidget);
    expect(find.text('Review: Pending'), findsOneWidget);
  });

  testWidgets('displays failed-safe workflow state', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: ItineraryPage(
        api: _TripApi(itinerary: _itinerary()),
        tripId: 'trip-1',
        workflowSource: _WorkflowSource(_workflow(AgentWorkflowStatus.failedSafe)),
      ),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Status: Failed safely'), findsOneWidget);
    expect(find.text('Workflow failed safely.'), findsOneWidget);
  });

  testWidgets('keeps itinerary visible when workflow is unavailable', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: ItineraryPage(
        api: _TripApi(itinerary: _itinerary()),
        tripId: 'trip-1',
        workflowSource: _WorkflowSource(null),
      ),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Itinerary'), findsOneWidget);
    expect(find.text('Workflow not available yet.'), findsOneWidget);
  });
}
