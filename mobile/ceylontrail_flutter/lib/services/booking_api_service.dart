import '../models/booking_model.dart';
import 'api_client.dart';

class BookingApiService {
  const BookingApiService(this._client);

  final ApiClient _client;

  Future<List<AvailabilitySlotModel>> fetchAvailabilitySlots({required String attractionId}) async {
    final response = await _client.get('/api/bookings/availability-slots', queryParameters: {'attractionId': attractionId});
    final data = response.data;
    if (data is! List) return [];
    return data
        .whereType<Map>()
        .map((json) => AvailabilitySlotModel.fromJson(Map<String, dynamic>.from(json)))
        .toList();
  }

  Future<BookingModel> createBooking({String? tripId, required String availabilitySlotId, required int numberOfGuests}) async {
    final response = await _client.post('/api/bookings', data: createBookingPayload(tripId: tripId, availabilitySlotId: availabilitySlotId, numberOfGuests: numberOfGuests));
    return BookingModel.fromJson(Map<String, dynamic>.from(response.data as Map));
  }

  Future<List<BookingModel>> fetchMyBookings() async {
    final response = await _client.get('/api/bookings');
    final data = response.data;
    if (data is! List) return [];
    return data
        .whereType<Map>()
        .map((json) => BookingModel.fromJson(Map<String, dynamic>.from(json)))
        .toList();
  }

  Future<BookingModel> cancelBooking(String bookingId, String reason) async {
    final response = await _client.post(
      '/api/bookings/$bookingId/cancel',
      data: {'reason': reason},
    );
    return BookingModel.fromJson(Map<String, dynamic>.from(response.data as Map));
  }

  Future<void> deleteBooking(String bookingId) async {
    await _client.delete('/api/bookings/$bookingId');
  }
}

Map<String, dynamic> createBookingPayload({String? tripId, required String availabilitySlotId, required int numberOfGuests}) => {
  if (tripId != null && tripId.trim().isNotEmpty) 'tripId': tripId,
  'items': [{'availabilitySlotId': availabilitySlotId, 'numberOfGuests': numberOfGuests}],
};
