class TripPreference {
  const TripPreference({
    required this.id,
    required this.preferenceType,
    required this.value,
  });

  final String id;
  final String preferenceType;
  final String value;

  factory TripPreference.fromJson(Map<String, dynamic> json) => TripPreference(
    id: json['id']?.toString() ?? '',
    preferenceType: json['preferenceType'] as String? ?? '',
    value: json['value'] as String? ?? '',
  );
}

class Trip {
  const Trip({
    required this.id,
    required this.name,
    required this.startDate,
    required this.endDate,
    required this.budget,
    required this.status,
    required this.createdAt,
    required this.updatedAt,
    required this.preferences,
  });

  final String id;
  final String name;
  final DateTime? startDate;
  final DateTime? endDate;
  final double budget;
  final String status;
  final DateTime? createdAt;
  final DateTime? updatedAt;
  final List<TripPreference> preferences;

  factory Trip.fromJson(Map<String, dynamic> json) => Trip(
    id: json['id']?.toString() ?? '',
    name: json['name'] as String? ?? '',
    startDate: _date(json['startDate']),
    endDate: _date(json['endDate']),
    budget: _number(json['budget']),
    status: json['status'] as String? ?? '',
    createdAt: _date(json['createdAt']),
    updatedAt: _date(json['updatedAt']),
    preferences: (json['preferences'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(TripPreference.fromJson)
        .toList(),
  );

  static DateTime? _date(Object? value) =>
      value is String ? DateTime.tryParse(value) : null;

  static double _number(Object? value) => switch (value) {
    num number => number.toDouble(),
    String text => double.tryParse(text) ?? 0,
    _ => 0,
  };
}
