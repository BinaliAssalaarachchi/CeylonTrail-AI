import '../models/booking_model.dart';
import 'api_client.dart';

class BookingApiService {
  const BookingApiService(this._client);

  final ApiClient _client;

  Future<List<AvailabilitySlotModel>> fetchAvailabilitySlots({required String attractionId}) async {
    final response = await _client.get('/api/bookings/availability-slots', queryParameters: {'attractionId': attractionId});
    return (response.data as List<dynamic>).whereType<Map<String, dynamic>>().map(AvailabilitySlotModel.fromJson).toList();
  }

  Future<BookingModel> createBooking({String? tripId, required String availabilitySlotId, required int numberOfGuests}) async {
    final response = await _client.post('/api/bookings', data: createBookingPayload(tripId: tripId, availabilitySlotId: availabilitySlotId, numberOfGuests: numberOfGuests));
    return BookingModel.fromJson(response.data as Map<String, dynamic>);
  }

  Future<List<BookingModel>> fetchMyBookings() async {
    final response = await _client.get('/api/bookings');
    final list = response.data as List<dynamic>;
    return list
        .map((json) => BookingModel.fromJson(json as Map<String, dynamic>))
        .toList();
  }

  Future<BookingModel> cancelBooking(String bookingId, String reason) async {
    final response = await _client.post(
      '/api/bookings/$bookingId/cancel',
      data: {'reason': reason},
    );
    return BookingModel.fromJson(response.data as Map<String, dynamic>);
  }
}

Map<String, dynamic> createBookingPayload({String? tripId, required String availabilitySlotId, required int numberOfGuests}) => {
  if (tripId != null && tripId.trim().isNotEmpty) 'tripId': tripId,
  'items': [{'availabilitySlotId': availabilitySlotId, 'numberOfGuests': numberOfGuests}],
};
