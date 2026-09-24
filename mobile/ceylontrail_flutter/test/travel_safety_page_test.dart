import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ceylontrail_flutter/models/travel_intelligence_outcome_model.dart';
import 'package:ceylontrail_flutter/services/travel_intelligence_api_service.dart';
import 'package:ceylontrail_flutter/views/travel_safety_page.dart';

class _FakeSource implements TravelIntelligenceOutcomeSource {
  _FakeSource(this.outcome);
  final TravelIntelligenceOutcome? outcome;
  @override
  Future<TravelIntelligenceOutcome?> fetchLatest(String tripId) async => outcome;
}

TravelIntelligenceOutcome _outcome(TouristReviewStatus status) => TravelIntelligenceOutcome(
  tripId: 'trip-1', executionId: 'execution-1', riskLevel: TravelRiskLevel.medium,
  isFeasible: true, recommendedAction: TravelRecommendedAction.proceed,
  summary: 'Keep an eye on the local conditions.', requiresHumanApproval: status != TouristReviewStatus.notRequired,
  recommendations: const [], affectedItems: const [], alternatives: const [], safeWindows: const [],
  reviewStatus: status, decision: null, requestedAt: null, decidedAt: null, assessedAt: DateTime(2026, 9, 24),
);

Widget _page(TravelIntelligenceOutcome? outcome) => MaterialApp(home: TravelSafetyPage(tripId: 'trip-1', source: _FakeSource(outcome)));

void main() {
  testWidgets('shows no-assessment state for a 404-mapped null outcome', (tester) async {
    await tester.pumpWidget(_page(null));
    await tester.pumpAndSettle();
    expect(find.text('Travel safety assessment not available yet'), findsOneWidget);
    expect(find.text('Refresh'), findsOneWidget);
  });

  for (final status in [TouristReviewStatus.pending, TouristReviewStatus.approved, TouristReviewStatus.rejected, TouristReviewStatus.notRequired]) {
    testWidgets('shows ${status.name} review state', (tester) async {
      await tester.pumpWidget(_page(_outcome(status)));
      await tester.pumpAndSettle();
      expect(find.text(_expectedTitle(status)), findsOneWidget);
    });
  }
}

String _expectedTitle(TouristReviewStatus status) => switch (status) {
  TouristReviewStatus.pending => 'Awaiting Travel Coordinator Review',
  TouristReviewStatus.approved => 'Approved by Travel Coordinator',
  TouristReviewStatus.rejected => 'Not Approved',
  TouristReviewStatus.notRequired => 'Human review not required',
  _ => throw StateError('Unexpected test status'),
};
