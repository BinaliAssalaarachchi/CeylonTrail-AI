import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'package:ceylontrail_flutter/models/auth_user.dart';
import 'package:ceylontrail_flutter/models/trip_model.dart';
import 'package:ceylontrail_flutter/services/api_client.dart';
import 'package:ceylontrail_flutter/services/auth_storage.dart';
import 'package:ceylontrail_flutter/services/trip_api_service.dart';
import 'package:ceylontrail_flutter/views/trip_form_page.dart';

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
  _TripApi({this.failPreference = false})
    : super(ApiClient(storage: _Storage()));

  final bool failPreference;
  final calls = <String>[];
  var preferenceAttempts = 0;

  @override
  Future<Trip> createTrip({
    required String name,
    required DateTime startDate,
    required DateTime endDate,
    required double budget,
  }) async {
    calls.add('create');
    return Trip(
      id: 'trip-1',
      name: name,
      startDate: startDate,
      endDate: endDate,
      budget: budget,
      status: 'Draft',
      createdAt: DateTime(2026),
      updatedAt: DateTime(2026),
      preferences: const [],
    );
  }

  @override
  Future<TripPreference> addPreference({
    required String tripId,
    required String preferenceType,
    required String value,
  }) async {
    calls.add('preference:$preferenceType');
    preferenceAttempts++;
    if (failPreference && preferenceAttempts == 1) {
      throw const ApiException('preference unavailable');
    }
    return const TripPreference(
      id: 'preference-1',
      preferenceType: 'Objective',
      value: 'Explore culture',
    );
  }

  @override
  Future<Trip> updateTrip({
    required String id,
    required String name,
    required DateTime startDate,
    required DateTime endDate,
    required double budget,
    String? status,
  }) async {
    calls.add('update');
    return Trip(
      id: id,
      name: name,
      startDate: startDate,
      endDate: endDate,
      budget: budget,
      status: status ?? 'Draft',
      createdAt: DateTime(2026),
      updatedAt: DateTime(2026),
      preferences: const [],
    );
  }
}

Widget _form(_TripApi api, {Trip? trip}) {
  final router = GoRouter(
    initialLocation: '/create',
    routes: [
      GoRoute(
        path: '/create',
        builder: (context, state) => TripFormPage(
          api: api,
          trip: trip,
          initialStartDate: DateTime(2026, 10, 1),
          initialEndDate: DateTime(2026, 10, 3),
        ),
      ),
      GoRoute(
        path: '/trips/:id',
        builder: (context, state) => const Text('Trip details destination'),
      ),
    ],
  );
  return MaterialApp.router(routerConfig: router);
}

Future<void> _fillCreateForm(
  WidgetTester tester, {
  String objective = 'Explore culture',
}) async {
  final fields = find.byType(TextField);
  await tester.enterText(fields.at(0), 'Cultural escape');
  await tester.enterText(fields.at(1), objective);
  await tester.enterText(fields.at(2), '10000');
}

void main() {
  testWidgets('create mode shows and requires a trip objective', (
    tester,
  ) async {
    final api = _TripApi();
    await tester.pumpWidget(_form(api));
    await tester.pumpAndSettle();

    expect(find.text('Trip Objective'), findsOneWidget);
    await tester.enterText(find.byType(TextField).at(0), 'Cultural escape');
    await tester.enterText(find.byType(TextField).at(2), '10000');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Create trip'));
    await tester.pumpAndSettle();

    expect(find.text('Enter a trip objective.'), findsOneWidget);
    expect(api.calls, isEmpty);
  });

  testWidgets(
    'creates before saving Objective and navigates after both succeed',
    (tester) async {
      final api = _TripApi();
      await tester.pumpWidget(_form(api));
      await tester.pumpAndSettle();
      await _fillCreateForm(tester);
      await tester.tap(find.widgetWithText(ElevatedButton, 'Create trip'));
      await tester.pumpAndSettle();

      expect(api.calls, ['create', 'preference:Objective']);
      expect(find.text('Trip details destination'), findsOneWidget);
    },
  );

  testWidgets('retries only the Objective preference after partial success', (
    tester,
  ) async {
    final api = _TripApi(failPreference: true);
    await tester.pumpWidget(_form(api));
    await tester.pumpAndSettle();
    await _fillCreateForm(tester);
    await tester.tap(find.widgetWithText(ElevatedButton, 'Create trip'));
    await tester.pumpAndSettle();

    expect(
      find.textContaining(
        'Trip created, but the objective could not be saved.',
      ),
      findsOneWidget,
    );
    expect(api.calls, ['create', 'preference:Objective']);
    await tester.tap(find.widgetWithText(ElevatedButton, 'Create trip'));
    await tester.pumpAndSettle();

    expect(api.calls, [
      'create',
      'preference:Objective',
      'preference:Objective',
    ]);
    expect(find.text('Trip details destination'), findsOneWidget);
  });

  testWidgets(
    'edit mode remains an update-only flow without Objective saving',
    (tester) async {
      final api = _TripApi();
      final trip = Trip(
        id: 'trip-1',
        name: 'Existing trip',
        startDate: DateTime.now(),
        endDate: DateTime.now().add(const Duration(days: 2)),
        budget: 10000,
        status: 'Draft',
        createdAt: DateTime(2026),
        updatedAt: DateTime(2026),
        preferences: const [],
      );
      await tester.pumpWidget(_form(api, trip: trip));
      await tester.pumpAndSettle();

      expect(find.text('Trip Objective'), findsNothing);
      expect(find.text('Save changes'), findsOneWidget);
      expect(api.calls, isEmpty);
    },
  );
}
