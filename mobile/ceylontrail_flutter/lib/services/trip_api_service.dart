import '../models/trip_model.dart';
import 'api_client.dart';

class TripApiService {
  const TripApiService(this._client);

  final ApiClient _client;

  Future<List<Trip>> getTrips() async {
    final response = await _client.get('/api/trips');
    return (response.data as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(Trip.fromJson)
        .toList();
  }

  Future<Trip> getTrip(String id) async {
    final response = await _client.get('/api/trips/$id');
    return Trip.fromJson(response.data as Map<String, dynamic>);
  }

  Future<Trip> createTrip({required String name, required DateTime startDate, required DateTime endDate, required double budget}) async {
    final response = await _client.post('/api/trips', data: {
      'name': name.trim(),
      'startDate': dateValue(startDate),
      'endDate': dateValue(endDate),
      'budget': budget,
    });
    return Trip.fromJson(response.data as Map<String, dynamic>);
  }

  Future<Trip> updateTrip({required String id, required String name, required DateTime startDate, required DateTime endDate, required double budget, String? status}) async {
    final response = await _client.put('/api/trips/$id', data: {
      'name': name.trim(),
      'startDate': dateValue(startDate),
      'endDate': dateValue(endDate),
      'budget': budget,
      'status': ?status,
    });
    return Trip.fromJson(response.data as Map<String, dynamic>);
  }

  Future<void> deleteTrip(String id) async {
    await _client.delete('/api/trips/$id');
  }

  Future<TripPreference> addPreference({required String tripId, required String preferenceType, required String value}) async {
    final response = await _client.post('/api/trips/$tripId/preferences', data: {
      'preferenceType': preferenceType.trim(),
      'value': value.trim(),
    });
    return TripPreference.fromJson(response.data as Map<String, dynamic>);
  }

  Future<Itinerary?> getItinerary(String tripId) async {
    try {
      final response = await _client.get('/api/trips/$tripId/itinerary');
      return Itinerary.fromJson(response.data as Map<String, dynamic>);
    } on ApiException catch (error) {
      if (error.statusCode == 404) return null;
      rethrow;
    }
  }
}

String dateValue(DateTime date) => '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
