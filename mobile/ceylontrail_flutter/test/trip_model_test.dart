import 'package:flutter_test/flutter_test.dart';

import 'package:ceylontrail_flutter/models/trip_model.dart';
import 'package:ceylontrail_flutter/services/trip_api_service.dart';

void main() {
  test('parses calendar dates and money without timezone shifting', () {
    final trip = Trip.fromJson({
      'id': 't1', 'name': 'Hill country', 'startDate': '2026-10-10', 'endDate': '2026-10-12',
      'budget': '12500.50', 'status': 'Draft', 'createdAt': '2026-09-01T10:00:00Z', 'updatedAt': '2026-09-01T10:00:00Z',
      'preferences': [{'id': 'p1', 'preferenceType': 'Interest', 'value': 'Culture'}],
    });

    expect(trip.startDate, DateTime(2026, 10, 10));
    expect(trip.endDate.day, 12);
    expect(trip.budget, 12500.50);
    expect(trip.preferences.single.value, 'Culture');
  });

  test('parses and sorts nested itinerary days and trims TimeOnly values', () {
    final itinerary = Itinerary.fromJson({
      'id': 'i1', 'tripId': 't1', 'status': 'Ready', 'totalEstimatedCost': 5000,
      'createdAt': '2026-10-01T00:00:00Z', 'updatedAt': '2026-10-01T00:00:00Z',
      'days': [
        {'id': 'd2', 'dayNumber': 2, 'date': '2026-10-11', 'items': []},
        {'id': 'd1', 'dayNumber': 1, 'date': '2026-10-10', 'items': [{'id': 'x', 'attractionId': 'a1', 'startTime': '09:30:00', 'endTime': '11:00:00', 'estimatedCost': '250', 'notes': 'Bring water'}]},
      ],
    });

    expect(itinerary.days.first.dayNumber, 1);
    expect(itinerary.days.first.items.single.startTime, '09:30');
    expect(itinerary.days.first.items.single.estimatedCost, 250);
  });

  test('parses optional authoritative attraction details and coordinates', () {
    final item = ItineraryItem.fromJson({
      'id': 'x', 'attractionId': 'a1', 'startTime': '06:00:00', 'endTime': '09:00:00',
      'estimatedCost': 6500, 'attractionName': 'Sigiriya Heritage Sunrise Trail',
      'description': 'A guided sunrise walk.', 'address': 'Sigiriya, Matale, Sri Lanka',
      'district': 'Matale', 'category': 'Historical & Cultural', 'latitude': 7.957,
      'longitude': 80.7603,
    });

    expect(item.attractionName, 'Sigiriya Heritage Sunrise Trail');
    expect(item.district, 'Matale');
    expect(item.latitude, 7.957);
    expect(item.longitude, 80.7603);
  });

  test('serializes calendar dates in the API format', () {
    expect(dateValue(DateTime(2026, 1, 5)), '2026-01-05');
  });

  test('parses itinerary history status, cost, and timestamps', () {
    final history = ItineraryHistoryItem.fromJson({
      'id': 'i2', 'status': 'Superseded', 'totalEstimatedCost': '52000',
      'createdAt': '2026-09-23T10:00:00Z', 'updatedAt': '2026-09-23T10:00:00Z', 'dayCount': 4,
    });

    expect(history.id, 'i2');
    expect(history.status, 'Superseded');
    expect(history.totalEstimatedCost, 52000);
    expect(history.dayCount, 4);
  });
}
