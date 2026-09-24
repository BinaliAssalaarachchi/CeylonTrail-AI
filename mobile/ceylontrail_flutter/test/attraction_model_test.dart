import 'package:flutter_test/flutter_test.dart';

import 'package:ceylontrail_flutter/models/attraction_model.dart';

void main() {
  test('parses attraction details and nested collections', () {
    final attraction = AttractionModel.fromJson({
      'id': 'a1',
      'providerId': 'p1',
      'categoryId': 'c1',
      'name': 'Sigiriya Walk',
      'description': 'A cultural experience',
      'district': 'Matale',
      'address': 'Rock Road',
      'latitude': 7.95,
      'longitude': 80.75,
      'price': 2500,
      'status': 'Approved',
      'isActive': true,
      'createdAt': '2026-01-01T00:00:00Z',
      'updatedAt': '2026-01-02T00:00:00Z',
      'category': {'id': 'c1', 'name': 'Historical Sites'},
      'schedules': [
        {'id': 's1', 'attractionId': 'a1', 'dayOfWeek': 'Monday', 'openingTime': '09:00:00', 'closingTime': '17:00:00', 'isClosed': false},
      ],
      'experienceSlots': [
        {'id': 'x1', 'attractionId': 'a1', 'date': '2026-10-15', 'startTime': '10:00:00', 'endTime': '12:00:00', 'capacity': 20, 'availableCapacity': 8},
      ],
      'images': [],
      'isFavorite': true,
    });

    expect(attraction.name, 'Sigiriya Walk');
    expect(attraction.category?.name, 'Historical Sites');
    expect(attraction.schedules.single.dayOfWeek, 'Monday');
    expect(attraction.experienceSlots.single.availableCapacity, 8);
    expect(attraction.isFavorite, isTrue);
  });

  test('parses paginated and availability responses safely', () {
    final page = AttractionSearchResponse.fromJson({'items': [], 'totalCount': 0, 'page': 1, 'pageSize': 10, 'totalPages': 0});
    final availability = AvailabilityModel.fromJson({'attractionId': 'a1', 'date': null, 'slots': []});

    expect(page.items, isEmpty);
    expect(page.totalPages, 0);
    expect(availability.attractionId, 'a1');
    expect(availability.slots, isEmpty);
  });
}
