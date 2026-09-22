import '../models/travel_alert_model.dart';
import 'api_client.dart';

class TravelAlertApiService {
  const TravelAlertApiService(this._client);

  final ApiClient _client;

  Future<TravelAlertPage> fetchAlerts({
    String? district,
    TravelAlertSeverity? severity,
    TravelAlertStatus? status,
    int pageSize = 50,
  }) async {
    final response = await _client.get(
      '/api/travel-alerts',
      queryParameters: {
        if (status != null) 'status': status.apiValue,
        if (district != null && district.isNotEmpty) 'district': district,
        if (severity != null) 'severity': severity.apiValue,
        'sortBy': 'startDateTime',
        'sortDirection': 'desc',
        'page': 1,
        'pageSize': pageSize,
      },
    );
    return TravelAlertPage.fromJson(response.data as Map<String, dynamic>);
  }

  Future<TravelAlert> fetchAlert(String id) async {
    final response = await _client.get('/api/travel-alerts/$id');
    return TravelAlert.fromJson(response.data as Map<String, dynamic>);
  }
}

extension on TravelAlertSeverity {
  String get apiValue => name[0].toUpperCase() + name.substring(1);
}

extension on TravelAlertStatus {
  String get apiValue => name[0].toUpperCase() + name.substring(1);
}
