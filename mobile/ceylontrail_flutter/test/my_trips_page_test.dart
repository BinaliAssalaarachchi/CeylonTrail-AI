import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ceylontrail_flutter/models/auth_user.dart';
import 'package:ceylontrail_flutter/models/trip_model.dart';
import 'package:ceylontrail_flutter/services/api_client.dart';
import 'package:ceylontrail_flutter/services/auth_storage.dart';
import 'package:ceylontrail_flutter/services/trip_api_service.dart';
import 'package:ceylontrail_flutter/views/my_trips_page.dart';

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
  _TripApi(this.trips, this.itineraries)
    : super(ApiClient(storage: _Storage()));

  final List<Trip> trips;
  final Map<String, Itinerary?> itineraries;

  @override
  Future<List<Trip>> getTrips() async => trips;

  @override
  Future<Itinerary?> getItinerary(String tripId) async => itineraries[tripId];
}

Trip _trip(String id, String name, String status) => Trip(
  id: id,
  name: name,
  startDate: DateTime(2026, 10, 1),
  endDate: DateTime(2026, 10, 3),
  budget: 10000,
  status: status,
  createdAt: DateTime(2026, 9, 1),
  updatedAt: DateTime(2026, 9, 1),
  preferences: const [],
);

Itinerary _itinerary(String tripId, String status) => Itinerary(
  id: 'itinerary-$tripId',
  tripId: tripId,
  status: status,
  totalEstimatedCost: 5000,
  createdAt: DateTime(2026, 9, 2),
  updatedAt: DateTime(2026, 9, 2),
  days: const [],
);

Future<void> _pumpTrips(
  WidgetTester tester,
  Trip trip,
  Itinerary? itinerary,
  Size size,
) async {
  await tester.binding.setSurfaceSize(size);
  await tester.pumpWidget(
    MaterialApp(home: MyTripsPage(api: _TripApi([trip], {trip.id: itinerary}))),
  );
  await tester.pump(const Duration(milliseconds: 100));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('wide cards grow for long names and preserve itinerary actions', (
    tester,
  ) async {
    final trip = _trip(
      'long',
      'A very long Sri Lanka journey title that must remain readable',
      'Planned',
    );
    await _pumpTrips(
      tester,
      trip,
      _itinerary(trip.id, 'PendingHumanApproval'),
      const Size(900, 800),
    );

    await tester.scrollUntilVisible(find.text('View itinerary'), 300);
    expect(find.text('View itinerary'), findsOneWidget);
    expect(find.text('Being reviewed'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('mobile cards preserve planning state without overflow', (
    tester,
  ) async {
    final trip = _trip('new', 'A newly created trip', 'Draft');
    await _pumpTrips(tester, trip, null, const Size(390, 800));

    await tester.scrollUntilVisible(find.text('Plan itinerary'), 300);
    expect(find.text('Plan itinerary'), findsOneWidget);
    expect(find.text('Planning'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('completed itinerary remains accessible from the trip list', (
    tester,
  ) async {
    final trip = _trip('done', 'Completed journey', 'Completed');
    await _pumpTrips(
      tester,
      trip,
      _itinerary(trip.id, 'Completed'),
      const Size(900, 800),
    );

    await tester.scrollUntilVisible(find.text('View itinerary'), 300);
    expect(find.text('View itinerary'), findsOneWidget);
    expect(find.text('Completed'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
