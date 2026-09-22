import '../models/booking_model.dart';
import 'api_client.dart';

class BookingApiService {
  const BookingApiService(this._client);

  final ApiClient _client;

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
