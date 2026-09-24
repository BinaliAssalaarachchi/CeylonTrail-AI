enum TravelRiskLevel { low, medium, high, critical, unknown }

enum TravelRecommendedAction {
  proceed,
  proceedWithCaution,
  reschedule,
  reroute,
  reviewBudget,
  resolveScheduleConflict,
  manualReview,
  unknown,
}

enum TouristReviewStatus {
  notRequired,
  approvalRequired,
  pending,
  approved,
  rejected,
  unknown,
}

enum TravelDecision { approved, rejected, unknown }

enum TravelSafetyStatus {
  conditionallySafe,
  manualReviewRequired,
  notAvailable,
  unknown,
}

class TravelIntelligenceOutcome {
  const TravelIntelligenceOutcome({
    required this.tripId,
    required this.executionId,
    required this.riskLevel,
    required this.isFeasible,
    required this.recommendedAction,
    required this.summary,
    required this.requiresHumanApproval,
    required this.recommendations,
    required this.affectedItems,
    required this.alternatives,
    required this.safeWindows,
    required this.reviewStatus,
    required this.decision,
    required this.requestedAt,
    required this.decidedAt,
    required this.assessedAt,
  });

  final String tripId;
  final String executionId;
  final TravelRiskLevel riskLevel;
  final bool isFeasible;
  final TravelRecommendedAction recommendedAction;
  final String summary;
  final bool requiresHumanApproval;
  final List<TravelRecommendation> recommendations;
  final List<TravelAffectedItem> affectedItems;
  final List<TravelAlternative> alternatives;
  final List<TravelSafeWindow> safeWindows;
  final TouristReviewStatus reviewStatus;
  final TravelDecision? decision;
  final DateTime? requestedAt;
  final DateTime? decidedAt;
  final DateTime? assessedAt;

  factory TravelIntelligenceOutcome.fromJson(Map<String, dynamic> json) =>
      TravelIntelligenceOutcome(
        tripId: json['tripId']?.toString() ?? '',
        executionId: json['executionId']?.toString() ?? '',
        riskLevel: _enumValue(json['riskLevel'], TravelRiskLevel.values, TravelRiskLevel.unknown),
        isFeasible: json['isFeasible'] as bool? ?? false,
        recommendedAction: _enumValue(
          json['recommendedAction'],
          TravelRecommendedAction.values,
          TravelRecommendedAction.unknown,
        ),
        summary: json['summary'] as String? ?? '',
        requiresHumanApproval: json['requiresHumanApproval'] as bool? ?? false,
        recommendations: _objects(json['recommendations'], TravelRecommendation.fromJson),
        affectedItems: _objects(json['affectedItems'], TravelAffectedItem.fromJson),
        alternatives: _objects(json['alternatives'], TravelAlternative.fromJson),
        safeWindows: _objects(json['safeWindows'], TravelSafeWindow.fromJson),
        reviewStatus: _enumValue(json['reviewStatus'], TouristReviewStatus.values, TouristReviewStatus.unknown),
        decision: _decisionValue(json['decision']),
        requestedAt: _date(json['requestedAt']),
        decidedAt: _date(json['decidedAt']),
        assessedAt: _date(json['assessedAt']),
      );
}

class TravelRecommendation {
  const TravelRecommendation({required this.action, required this.explanation, required this.affectedItemReferences});

  final TravelRecommendedAction action;
  final String explanation;
  final List<String> affectedItemReferences;

  factory TravelRecommendation.fromJson(Map<String, dynamic> json) => TravelRecommendation(
    action: _enumValue(json['action'], TravelRecommendedAction.values, TravelRecommendedAction.unknown),
    explanation: json['explanation'] as String? ?? '',
    affectedItemReferences: _strings(json['affectedItemReferences']),
  );
}

class TravelAffectedItem {
  const TravelAffectedItem({required this.itemReference, required this.title, required this.district, required this.startDateTime, required this.endDateTime, required this.isBlocking});

  final String itemReference;
  final String? title;
  final String? district;
  final DateTime? startDateTime;
  final DateTime? endDateTime;
  final bool isBlocking;

  factory TravelAffectedItem.fromJson(Map<String, dynamic> json) => TravelAffectedItem(
    itemReference: json['itemReference']?.toString() ?? '',
    title: json['title'] as String?,
    district: json['district'] as String?,
    startDateTime: _date(json['startDateTime']),
    endDateTime: _date(json['endDateTime']),
    isBlocking: json['isBlocking'] as bool? ?? false,
  );
}

class TravelAlternative {
  const TravelAlternative({required this.action, required this.affectedItemReferences, required this.rationale, required this.safetyStatus, required this.requiresHumanApproval, required this.constraints});

  final TravelRecommendedAction action;
  final List<String> affectedItemReferences;
  final String rationale;
  final TravelSafetyStatus safetyStatus;
  final bool requiresHumanApproval;
  final List<String> constraints;

  factory TravelAlternative.fromJson(Map<String, dynamic> json) => TravelAlternative(
    action: _enumValue(json['action'], TravelRecommendedAction.values, TravelRecommendedAction.unknown),
    affectedItemReferences: _strings(json['affectedItemReferences']),
    rationale: json['rationale'] as String? ?? '',
    safetyStatus: _enumValue(json['safetyStatus'], TravelSafetyStatus.values, TravelSafetyStatus.unknown),
    requiresHumanApproval: json['requiresHumanApproval'] as bool? ?? false,
    constraints: _strings(json['constraints']),
  );
}

class TravelSafeWindow {
  const TravelSafeWindow({required this.itemReference, required this.proposedStart, required this.proposedEnd, required this.reason, required this.safetyStatus, required this.constraints});

  final String itemReference;
  final DateTime? proposedStart;
  final DateTime? proposedEnd;
  final String reason;
  final TravelSafetyStatus safetyStatus;
  final List<String> constraints;

  factory TravelSafeWindow.fromJson(Map<String, dynamic> json) => TravelSafeWindow(
    itemReference: json['itemReference']?.toString() ?? '',
    proposedStart: _date(json['proposedStart']),
    proposedEnd: _date(json['proposedEnd']),
    reason: json['reason'] as String? ?? '',
    safetyStatus: _enumValue(json['safetyStatus'], TravelSafetyStatus.values, TravelSafetyStatus.unknown),
    constraints: _strings(json['constraints']),
  );
}

DateTime? _date(Object? value) => value is String ? DateTime.tryParse(value) : null;

List<String> _strings(Object? value) => value is List
    ? value.whereType<String>().toList()
    : const [];

List<T> _objects<T>(Object? value, T Function(Map<String, dynamic>) parse) => value is List
    ? value.whereType<Map<String, dynamic>>().map(parse).toList()
    : <T>[];

T _enumValue<T>(Object? value, List<T> values, T fallback) {
  if (value is! String) return fallback;
  final normalized = _normalize(value);
  for (final item in values) {
    if (_normalize(item.toString().split('.').last) == normalized) return item;
  }
  return fallback;
}

TravelDecision? _decisionValue(Object? value) {
  if (value == null) return null;
  return _enumValue(value, TravelDecision.values, TravelDecision.unknown);
}

String _normalize(String value) => value.replaceAll(RegExp(r'[_\-\s]'), '').toLowerCase();
