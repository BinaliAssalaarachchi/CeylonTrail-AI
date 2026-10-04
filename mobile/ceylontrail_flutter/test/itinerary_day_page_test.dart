import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'package:ceylontrail_flutter/models/auth_user.dart';
import 'package:ceylontrail_flutter/models/agent_workflow_model.dart';
import 'package:ceylontrail_flutter/models/trip_model.dart';
import 'package:ceylontrail_flutter/services/api_client.dart';
import 'package:ceylontrail_flutter/services/auth_storage.dart';
import 'package:ceylontrail_flutter/services/trip_api_service.dart';
import 'package:ceylontrail_flutter/services/travel_intelligence_api_service.dart';
import 'package:ceylontrail_flutter/views/itinerary_day_page.dart';
import 'package:ceylontrail_flutter/views/itinerary_page.dart';

class _WorkflowSource implements AgentWorkflowSource {
  @override
  Future<AgentWorkflow?> fetchWorkflow(String tripId) async => null;
}

class _Storage implements AuthSessionStorage {
  @override
  Future<void> clear() async {}

  @override
  Future<StoredSession?> read() async => null;

  @override
  Future<void> write({
    required String token,
    required DateTime expiresAt,
    required AuthUser user,
  }) async {}
}

class _TripApi extends TripApiService {
  _TripApi(this.itinerary) : super(ApiClient(storage: _Storage()));
  final Itinerary itinerary;

  @override
  Future<Itinerary?> getItinerary(String tripId) async => itinerary;

  @override
  Future<List<ItineraryHistoryItem>> getItineraryHistory(String tripId) async =>
      const [];

  @override
  Future<Trip> getTrip(String id) async => Trip(
    id: id,
    name: 'Galle journey',
    startDate: DateTime(2026, 10, 3),
    endDate: DateTime(2026, 10, 5),
    budget: 10000,
    status: 'Planned',
    createdAt: null,
    updatedAt: null,
    preferences: const [],
  );
}

ItineraryItem _item() => const ItineraryItem(
  id: 'item-1',
  attractionId: 'galle-fort',
  startTime: '08:00',
  endTime: '10:00',
  estimatedCost: 3800,
  attractionName: 'Galle Fort Heritage Walk',
  description: 'A grounded heritage walk through the fort.',
  address: 'Galle Fort, Galle, Sri Lanka',
  district: 'Galle',
  category: 'Historical & Cultural',
);

Itinerary _itinerary({bool emptyDay = false}) => Itinerary(
  id: 'itinerary-1',
  tripId: 'trip-1',
  status: 'Generated',
  totalEstimatedCost: 3800,
  createdAt: null,
  updatedAt: null,
  days: [
    ItineraryDay(
      id: 'day-1',
      dayNumber: 1,
      date: DateTime(2026, 10, 3),
      items: [_item()],
    ),
    ItineraryDay(
      id: 'day-2',
      dayNumber: 2,
      date: DateTime(2026, 10, 4),
      items: emptyDay ? const [] : [_item()],
    ),
  ],
);

void main() {
  testWidgets('populated day renders authoritative attraction details', (
    tester,
  ) async {
    await tester.binding.setSurfaceSize(const Size(800, 1200));
    await tester.pumpWidget(
      MaterialApp(home: ItineraryDayPage(day: _itinerary().days.first)),
    );
    await tester.pumpAndSettle();

    expect(find.text('DAY 1'), findsOneWidget);
    expect(find.text('Galle Fort Heritage Walk'), findsOneWidget);
    expect(find.text('Historical & Cultural'), findsOneWidget);
    expect(find.text('Galle Fort, Galle, Sri Lanka, Galle'), findsOneWidget);
    expect(
      find.text('A grounded heritage walk through the fort.'),
      findsOneWidget,
    );
    expect(find.text('LKR 3,800'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('empty day renders a visible empty state', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ItineraryDayPage(day: _itinerary(emptyDay: true).days[1]),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('DAY 2'), findsOneWidget);
    expect(
      find.text('No activities are planned for this day.'),
      findsOneWidget,
    );
    expect(tester.takeException(), isNull);
  });

  testWidgets('overview navigates Day 1 and Day 2 using their day objects', (
    tester,
  ) async {
    final itinerary = _itinerary(emptyDay: true);
    final api = _TripApi(itinerary);
    final router = GoRouter(
      initialLocation: '/trips/trip-1/itinerary',
      routes: [
        GoRoute(
          path: '/trips/:id/itinerary',
          builder: (context, state) => ItineraryPage(
            api: api,
            tripId: state.pathParameters['id']!,
            workflowSource: _WorkflowSource(),
          ),
          routes: [
            GoRoute(
              path: 'day/:dayNumber',
              builder: (context, state) =>
                  ItineraryDayPage(day: state.extra! as ItineraryDay),
            ),
          ],
        ),
      ],
    );
    await tester.pumpWidget(MaterialApp.router(routerConfig: router));
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(find.text('Day 1'), 300);
    expect(find.text('Day 3'), findsWidgets);
    await tester.tap(find.widgetWithText(ActionChip, 'Day 1'));
    await tester.pumpAndSettle();
    expect(find.text('DAY 1'), findsOneWidget);
    expect(find.text('Galle Fort Heritage Walk'), findsOneWidget);

    router.pop();
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(find.text('Day 2'), 300);
    await tester.tap(find.widgetWithText(ActionChip, 'Day 2'));
    await tester.pumpAndSettle();
    expect(find.text('DAY 2'), findsOneWidget);
    expect(
      find.text('No activities are planned for this day.'),
      findsOneWidget,
    );

    router.pop();
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(find.text('Day 3'), 300);
    await tester.tap(find.widgetWithText(ActionChip, 'Day 3'));
    await tester.pumpAndSettle();
    expect(find.text('DAY 3'), findsOneWidget);
    expect(
      find.text('No activities are planned for this day.'),
      findsOneWidget,
    );
    expect(tester.takeException(), isNull);
  });
}
