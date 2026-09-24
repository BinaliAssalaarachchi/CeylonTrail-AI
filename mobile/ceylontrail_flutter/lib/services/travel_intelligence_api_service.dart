import 'package:dio/dio.dart';

import '../models/travel_intelligence_outcome_model.dart';
import 'api_client.dart';

abstract interface class TravelIntelligenceOutcomeSource {
  Future<TravelIntelligenceOutcome?> fetchLatest(String tripId);
}

class TravelIntelligenceApiService implements TravelIntelligenceOutcomeSource {
  const TravelIntelligenceApiService(this._client) : _request = null;

  const TravelIntelligenceApiService.forTesting(this._request)
      : _client = null;

  final ApiClient? _client;
  final Future<Response<dynamic>> Function(String path)? _request;

  @override
  Future<TravelIntelligenceOutcome?> fetchLatest(String tripId) async {
    try {
      final response = await (_request?.call('/api/trips/$tripId/travel-intelligence/latest') ??
          _client!.get('/api/trips/$tripId/travel-intelligence/latest'));
      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw const ApiException('Travel safety response was malformed.');
      }
      return TravelIntelligenceOutcome.fromJson(data);
    } on ApiException catch (error) {
      if (error.statusCode == 404) return null;
      rethrow;
    }
  }
}
