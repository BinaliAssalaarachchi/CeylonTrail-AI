enum TravelAlertType { weather, roadClosure, transport, safety, siteClosure, event, general }

enum TravelAlertSeverity { low, medium, high, critical }

enum TravelAlertStatus { draft, active, expired, cancelled }

class TravelAlert {
  const TravelAlert({
    required this.id,
    required this.title,
    required this.description,
    required this.alertType,
    required this.severity,
    required this.district,
    required this.startDateTime,
    required this.endDateTime,
    required this.status,
    required this.source,
    required this.createdByUserId,
    required this.createdByUserName,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String title;
  final String description;
  final TravelAlertType alertType;
  final TravelAlertSeverity severity;
  final String district;
  final DateTime? startDateTime;
  final DateTime? endDateTime;
  final TravelAlertStatus status;
  final String? source;
  final String? createdByUserId;
  final String? createdByUserName;
  final DateTime? createdAt;
  final DateTime? updatedAt;

  factory TravelAlert.fromJson(Map<String, dynamic> json) => TravelAlert(
    id: json['id']?.toString() ?? '',
    title: json['title'] as String? ?? '',
    description: json['description'] as String? ?? '',
    alertType: _enumValue(json['alertType'], TravelAlertType.values, TravelAlertType.general),
    severity: _enumValue(json['severity'], TravelAlertSeverity.values, TravelAlertSeverity.medium),
    district: json['district'] as String? ?? '',
    startDateTime: _date(json['startDateTime']),
    endDateTime: _date(json['endDateTime']),
    status: _enumValue(json['status'], TravelAlertStatus.values, TravelAlertStatus.active),
    source: json['source'] as String?,
    createdByUserId: json['createdByUserId']?.toString(),
    createdByUserName: json['createdByUserName'] as String?,
    createdAt: _date(json['createdAt']),
    updatedAt: _date(json['updatedAt']),
  );

  static DateTime? _date(Object? value) =>
      value is String ? DateTime.tryParse(value) : null;

  static T _enumValue<T>(Object? value, List<T> values, T fallback) {
    if (value is! String) return fallback;
    final normalized = value.replaceAll('_', '').toLowerCase();
    for (final item in values) {
      if (item.toString().split('.').last.toLowerCase() == normalized) return item;
    }
    return fallback;
  }
}

class TravelAlertPage {
  const TravelAlertPage({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<TravelAlert> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  factory TravelAlertPage.fromJson(Map<String, dynamic> json) => TravelAlertPage(
    items: (json['items'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(TravelAlert.fromJson)
        .toList(),
    page: json['page'] as int? ?? 1,
    pageSize: json['pageSize'] as int? ?? 20,
    totalCount: json['totalCount'] as int? ?? 0,
    totalPages: json['totalPages'] as int? ?? 0,
  );
}
