import 'package:ceylontrail_flutter/services/booking_api_service.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('booking payload uses the M3 slot and never sends client pricing', () {
    final payload = createBookingPayload(
      tripId: 'trip-1',
      availabilitySlotId: 'slot-1',
      numberOfGuests: 3,
    );

    expect(payload['tripId'], 'trip-1');
    expect(payload.containsKey('unitPrice'), isFalse);
    expect(payload['items'], [
      {'availabilitySlotId': 'slot-1', 'numberOfGuests': 3},
    ]);
  });

  test('booking payload can omit the optional trip', () {
    final payload = createBookingPayload(
      availabilitySlotId: 'slot-2',
      numberOfGuests: 1,
    );

    expect(payload.containsKey('tripId'), isFalse);
    expect(payload['items'], [
      {'availabilitySlotId': 'slot-2', 'numberOfGuests': 1},
    ]);
  });
}
