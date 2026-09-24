import '../models/trip_model.dart';
import 'api_client.dart';

class TripApiService {
  const TripApiService(this._client);

  final ApiClient _client;

  Future<List<Trip>> fetchTrips() async {
    final response = await _client.get('/api/trips');
    final data = response.data;
    if (data is! List<dynamic>) {
      throw const ApiException('Trips response was malformed.');
    }

    return data
        .whereType<Map<String, dynamic>>()
        .map(Trip.fromJson)
        .toList();
  }
}
