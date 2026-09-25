import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ceylontrail_flutter/services/travel_intelligence_api_service.dart';

void main() {
  test(
    'requests the tourist-safe latest assessment for the supplied trip',
    () async {
      String? requestedPath;
      final service = TravelIntelligenceApiService.forTesting((path) async {
        requestedPath = path;
        return Response<dynamic>(
          requestOptions: RequestOptions(path: path),
          data: {
            'tripId': 'trip-42',
            'executionId': 'execution-1',
            'riskLevel': 'Low',
            'isFeasible': true,
            'recommendedAction': 'Proceed',
            'summary': 'Safe to proceed.',
            'requiresHumanApproval': false,
            'recommendations': [],
            'affectedItems': [],
            'alternatives': [],
            'safeWindows': [],
            'reviewStatus': 'NotRequired',
            'decision': null,
          },
        );
      });

      final outcome = await service.fetchLatest('trip-42');

      expect(requestedPath, '/api/trips/trip-42/travel-intelligence/latest');
      expect(outcome?.tripId, 'trip-42');
    },
  );
}
