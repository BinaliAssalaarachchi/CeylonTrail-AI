import 'package:ceylontrail_flutter/models/booking_model.dart';
import 'package:ceylontrail_flutter/views/bookings_page.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('Draft bookings appear in Active', () {
    final draft = _booking('draft', 'Draft');

    expect(filterBookingsByTab([draft], 0), contains(draft));
  });

  test('backend pending statuses appear in Active', () {
    final pendingAi = _booking('pending-ai', 'PendingAI');
    final pendingHuman = _booking('pending-human', 'PendingHumanApproval');
    final legacyPending = _booking('legacy-pending', 'Pending');

    final active = filterBookingsByTab(
      [pendingAi, pendingHuman, legacyPending],
      0,
    );

    expect(active, containsAll([pendingAi, pendingHuman]));
    expect(active, isNot(contains(legacyPending)));
  });

  test('Confirmed bookings remain in Active and Confirmed', () {
    final confirmed = _booking('confirmed', 'Confirmed');

    expect(filterBookingsByTab([confirmed], 0), contains(confirmed));
    expect(filterBookingsByTab([confirmed], 1), contains(confirmed));
  });

  test('Cancelled and Rejected bookings remain in History', () {
    final cancelled = _booking('cancelled', 'Cancelled');
    final rejected = _booking('rejected', 'Rejected');
    final completed = _booking('completed', 'Completed');

    final history = filterBookingsByTab([cancelled, rejected, completed], 2);

    expect(history, containsAll([cancelled, rejected, completed]));
    expect(filterBookingsByTab([cancelled, rejected, completed], 0), isEmpty);
    expect(filterBookingsByTab([cancelled, rejected, completed], 1), isEmpty);
  });
}

BookingModel _booking(String id, String status) => BookingModel(
      id: id,
      userId: 'user-1',
      currentStatus: status,
      totalAmount: 6500,
      createdAt: DateTime.utc(2026, 9, 26),
      updatedAt: DateTime.utc(2026, 9, 26),
      items: const [],
      statusHistory: const [],
      cancellationRequests: const [],
    );
