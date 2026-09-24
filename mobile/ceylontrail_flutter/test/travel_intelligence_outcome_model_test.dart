import 'package:flutter_test/flutter_test.dart';

import 'package:ceylontrail_flutter/models/travel_intelligence_outcome_model.dart';
import 'package:ceylontrail_flutter/services/api_client.dart';
import 'package:ceylontrail_flutter/services/travel_intelligence_api_service.dart';

void main() {
  test('parses the complete tourist-safe outcome contract', () {
    final outcome = TravelIntelligenceOutcome.fromJson({
      'tripId': 'trip-1',
      'executionId': 'execution-1',
      'riskLevel': 'High',
      'isFeasible': false,
      'recommendedAction': 'Reschedule',
      'summary': 'Move the visit to a safer window.',
      'requiresHumanApproval': true,
      'recommendations': [{'action': 'Reschedule', 'explanation': 'Avoid the alert window.', 'affectedItemReferences': ['item-1']}],
      'affectedItems': [{'itemReference': 'item-1', 'title': 'Temple visit', 'district': 'Kandy', 'isBlocking': true}],
      'alternatives': [{'action': 'Reroute', 'rationale': 'Use a safer route.', 'safetyStatus': 'ConditionallySafe', 'requiresHumanApproval': false, 'affectedItemReferences': [], 'constraints': []}],
      'safeWindows': [{'itemReference': 'item-1', 'proposedStart': '2026-10-01T10:00:00Z', 'proposedEnd': '2026-10-01T12:00:00Z', 'reason': 'Lower risk period.', 'safetyStatus': 'ConditionallySafe', 'constraints': []}],
      'reviewStatus': 'Pending',
      'decision': null,
      'requestedAt': '2026-09-24T10:00:00Z',
      'decidedAt': null,
      'assessedAt': '2026-09-24T09:00:00Z',
    });

    expect(outcome.riskLevel, TravelRiskLevel.high);
    expect(outcome.recommendedAction, TravelRecommendedAction.reschedule);
    expect(outcome.affectedItems.single.isBlocking, isTrue);
    expect(outcome.alternatives.single.safetyStatus, TravelSafetyStatus.conditionallySafe);
    expect(outcome.safeWindows.single.proposedStart, isNotNull);
    expect(outcome.reviewStatus, TouristReviewStatus.pending);
  });

  test('handles nullable, empty, malformed, and unknown values safely', () {
    final outcome = TravelIntelligenceOutcome.fromJson({
      'tripId': 'trip-2',
      'riskLevel': 'FutureRisk',
      'recommendedAction': 'FutureAction',
      'recommendations': [],
      'affectedItems': [],
      'alternatives': [],
      'safeWindows': [],
      'reviewStatus': 'FutureReview',
      'decision': 'FutureDecision',
      'requestedAt': 'not-a-date',
      'decidedAt': null,
      'assessedAt': 'not-a-date',
    });

    expect(outcome.riskLevel, TravelRiskLevel.unknown);
    expect(outcome.recommendedAction, TravelRecommendedAction.unknown);
    expect(outcome.reviewStatus, TouristReviewStatus.unknown);
    expect(outcome.decision, TravelDecision.unknown);
    expect(outcome.requestedAt, isNull);
    expect(outcome.decidedAt, isNull);
    expect(outcome.assessedAt, isNull);
    expect(outcome.recommendations, isEmpty);
  });

  test('maps a 404 from the tourist endpoint to no assessment', () async {
    final service = TravelIntelligenceApiService.forTesting(
      (_) async => throw const ApiException('Not found', statusCode: 404),
    );

    expect(await service.fetchLatest('trip-1'), isNull);
  });
}
