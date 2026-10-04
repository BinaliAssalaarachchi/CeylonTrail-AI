import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ceylontrail_flutter/models/trip_model.dart';
import 'package:ceylontrail_flutter/widgets/itinerary_map.dart';

ItineraryItem _item({double? latitude, double? longitude}) => ItineraryItem(
  id: 'item-${latitude ?? 'none'}',
  attractionId: 'attraction-1',
  startTime: '08:00',
  endTime: '10:00',
  estimatedCost: 3800,
  attractionName: 'Galle Fort Heritage Walk',
  address: 'Galle Fort, Galle, Sri Lanka',
  latitude: latitude,
  longitude: longitude,
);

void main() {
  testWidgets('shows unavailable state when no itinerary coordinates exist', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: SizedBox(height: 300, child: ItineraryMapPanel(items: [])),
      ),
    );

    expect(
      find.text('Map locations are not available for this itinerary yet.'),
      findsOneWidget,
    );
  });

  testWidgets(
    'fails gracefully before platform map credentials are configured',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: SizedBox(
            height: 300,
            child: ItineraryMapPanel(
              items: [
                _item(latitude: 6.0329, longitude: 80.2168),
                _item(latitude: 6.4210, longitude: 80.0000),
              ],
            ),
          ),
        ),
      );

      expect(
        find.text('Interactive maps are not configured on this device yet.'),
        findsOneWidget,
      );
      expect(tester.takeException(), isNull);
    },
  );
}
