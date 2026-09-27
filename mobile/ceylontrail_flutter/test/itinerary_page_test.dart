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

  AgentWorkflow? workflow;
  var fetchCount = 0;

  @override
  Future<AgentWorkflow?> fetchWorkflow(String tripId) async {
    fetchCount++;
    return workflow;
  }
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

AgentWorkflow _workflow(
  AgentWorkflowStatus status, {
  String? reviewStatus,
  bool? executionSucceeded,
  String? bookingId,
  String safeMessage = '',
}) => AgentWorkflow(
      workflowId: 'workflow-1',
      tripId: 'trip-1',
      status: status,
      requiresApproval: status == AgentWorkflowStatus.awaitingApproval,
      reviewStatus: reviewStatus ?? (status == AgentWorkflowStatus.awaitingApproval ? 'Pending' : null),
      executionSucceeded: executionSucceeded,
      bookingId: bookingId,
      safeMessage: safeMessage,
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

  testWidgets('refreshes workflow and displays completed booking state', (tester) async {
    final source = _WorkflowSource(_workflow(AgentWorkflowStatus.awaitingApproval));
    await tester.pumpWidget(MaterialApp(
      home: ItineraryPage(
        api: _TripApi(itinerary: _itinerary()),
        tripId: 'trip-1',
        workflowSource: source,
      ),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Status: Awaiting approval'), findsOneWidget);
    expect(source.fetchCount, 1);

    source.workflow = _workflow(
      AgentWorkflowStatus.completed,
      executionSucceeded: true,
      bookingId: 'booking-1',
    );
    await tester.drag(find.byType(ListView), const Offset(0, 300));
    await tester.pumpAndSettle();

    expect(source.fetchCount, 2);
    expect(find.text('Status: Completed'), findsOneWidget);
    expect(find.text('Booking execution succeeded · Booking booking-1'), findsOneWidget);
    expect(find.textContaining('Planner:'), findsOneWidget);
    expect(find.textContaining('Destination:'), findsOneWidget);
    expect(find.textContaining('BookingAction:'), findsOneWidget);
    expect(find.textContaining('TravelIntelligence:'), findsOneWidget);
  });

  testWidgets('displays rejected and cancelled workflow state after refresh', (tester) async {
    final source = _WorkflowSource(_workflow(AgentWorkflowStatus.awaitingApproval));
    await tester.pumpWidget(MaterialApp(
      home: ItineraryPage(
        api: _TripApi(itinerary: _itinerary()),
        tripId: 'trip-1',
        workflowSource: source,
      ),
    ));
    await tester.pumpAndSettle();

    source.workflow = _workflow(
      AgentWorkflowStatus.cancelled,
      reviewStatus: 'Rejected',
    );
    await tester.drag(find.byType(ListView), const Offset(0, 300));
    await tester.pumpAndSettle();

    expect(find.text('Status: Cancelled'), findsOneWidget);
    expect(find.text('Review: Rejected'), findsOneWidget);
  });

  testWidgets('displays failed-safe workflow state', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: ItineraryPage(
        api: _TripApi(itinerary: _itinerary()),
        tripId: 'trip-1',
        workflowSource: _WorkflowSource(_workflow(
          AgentWorkflowStatus.failedSafe,
          executionSucceeded: false,
          safeMessage: 'Workflow failed safely.',
        )),
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
