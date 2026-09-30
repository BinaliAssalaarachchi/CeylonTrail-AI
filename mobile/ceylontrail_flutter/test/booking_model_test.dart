import 'package:ceylontrail_flutter/models/booking_model.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('parses the current booking response contract', () {
    final booking = BookingModel.fromJson({
      'id': 'booking-1',
      'tripId': null,
      'currentStatus': 'Draft',
      'totalAmount': 2500.0,
      'createdAt': '2026-09-25T08:00:00Z',
      'updatedAt': '2026-09-25T08:00:00Z',
      'items': [
        {'id': 'item-1', 'availabilitySlotId': 'slot-1', 'numberOfGuests': 2, 'unitPrice': 1250, 'subTotal': 2500},
      ],
      'statusHistory': [],
      'cancellationRequests': [],
    });

    expect(booking.currentStatus, 'Draft');
    expect(booking.items.single.availabilitySlotId, 'slot-1');
    expect(booking.items.single.numberOfGuests, 2);
    expect(booking.items.single.subtotal, 2500);
  });

  test('parses server availability slot pricing and capacity', () {
    final slot = AvailabilitySlotModel.fromJson({
      'id': 'slot-1',
      'attractionId': 'attraction-1',
      'startTime': '2026-09-26T10:00:00Z',
      'endTime': '2026-09-26T12:00:00Z',
      'maxCapacity': 10,
      'bookedCapacity': 4,
      'availableCapacity': 6,
      'pricePerPerson': 1250,
    });

    expect(slot.id, 'slot-1');
    expect(slot.availableCapacity, 6);
    expect(slot.pricePerPerson, 1250);
  });
}
