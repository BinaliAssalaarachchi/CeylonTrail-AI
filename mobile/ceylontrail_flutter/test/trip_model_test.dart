import 'package:flutter_test/flutter_test.dart';

import 'package:ceylontrail_flutter/models/trip_model.dart';

void main() {
  test('Trip parses the backend response fields', () {
    final trip = Trip.fromJson({
      'id': 'trip-1',
      'name': 'Kandy Escape',
      'startDate': '2026-10-01',
      'endDate': '2026-10-05',
      'budget': 125000.50,
      'status': 'Draft',
      'createdAt': '2026-09-24T10:00:00Z',
      'updatedAt': '2026-09-24T10:30:00Z',
      'preferences': [
        {'id': 'preference-1', 'preferenceType': 'Pace', 'value': 'Slow'},
      ],
    });

    expect(trip.id, 'trip-1');
    expect(trip.name, 'Kandy Escape');
    expect(trip.startDate, DateTime(2026, 10, 1));
    expect(trip.endDate, DateTime(2026, 10, 5));
    expect(trip.budget, 125000.50);
    expect(trip.status, 'Draft');
    expect(trip.preferences.single.value, 'Slow');
  });

  test('Trip safely handles nullable and malformed optional values', () {
    final trip = Trip.fromJson({
      'id': 'trip-2',
      'name': null,
      'startDate': 'not-a-date',
      'endDate': null,
      'budget': null,
      'status': null,
      'createdAt': null,
      'updatedAt': null,
      'preferences': null,
    });

    expect(trip.name, isEmpty);
    expect(trip.startDate, isNull);
    expect(trip.endDate, isNull);
    expect(trip.budget, 0);
    expect(trip.status, isEmpty);
    expect(trip.preferences, isEmpty);
  });
}
