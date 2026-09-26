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
  final DateTime startDate;
  final DateTime endDate;
  final double budget;
  final String status;
  final DateTime? createdAt;
  final DateTime? updatedAt;
  final List<TripPreference> preferences;

  factory Trip.fromJson(Map<String, dynamic> json) => Trip(
    id: json['id']?.toString() ?? '',
    name: json['name'] as String? ?? '',
    startDate: parseCalendarDate(json['startDate']),
    endDate: parseCalendarDate(json['endDate']),
    budget: parseMoney(json['budget']),
    status: json['status']?.toString() ?? 'Draft',
    createdAt: parseTimestamp(json['createdAt']),
    updatedAt: parseTimestamp(json['updatedAt']),
    preferences: (json['preferences'] as List<dynamic>? ?? const [])
        .whereType<Map>()
        .map((item) => TripPreference.fromJson(Map<String, dynamic>.from(item)))
        .toList(),
  );
}

class TripPreference {
  const TripPreference({required this.id, required this.preferenceType, required this.value});

  final String id;
  final String preferenceType;
  final String value;

  factory TripPreference.fromJson(Map<String, dynamic> json) => TripPreference(
    id: json['id']?.toString() ?? '',
    preferenceType: json['preferenceType'] as String? ?? '',
    value: json['value'] as String? ?? '',
  );
}

class Itinerary {
  const Itinerary({
    required this.id,
    required this.tripId,
    required this.status,
    required this.totalEstimatedCost,
    required this.createdAt,
    required this.updatedAt,
    required this.days,
  });

  final String id;
  final String tripId;
  final String status;
  final double totalEstimatedCost;
  final DateTime? createdAt;
  final DateTime? updatedAt;
  final List<ItineraryDay> days;

  factory Itinerary.fromJson(Map<String, dynamic> json) => Itinerary(
    id: json['id']?.toString() ?? '',
    tripId: json['tripId']?.toString() ?? '',
    status: json['status']?.toString() ?? '',
    totalEstimatedCost: parseMoney(json['totalEstimatedCost']),
    createdAt: parseTimestamp(json['createdAt']),
    updatedAt: parseTimestamp(json['updatedAt']),
    days: (json['days'] as List<dynamic>? ?? const [])
        .whereType<Map>()
        .map((item) => ItineraryDay.fromJson(Map<String, dynamic>.from(item)))
        .toList()
      ..sort((a, b) => a.dayNumber.compareTo(b.dayNumber)),
  );
}

class ItineraryHistoryItem {
  const ItineraryHistoryItem({required this.id, required this.status, required this.totalEstimatedCost, required this.createdAt, required this.updatedAt, required this.dayCount});
  final String id;
  final String status;
  final double totalEstimatedCost;
  final DateTime? createdAt;
  final DateTime? updatedAt;
  final int dayCount;
  factory ItineraryHistoryItem.fromJson(Map<String, dynamic> json) => ItineraryHistoryItem(
    id: json['id']?.toString() ?? '', status: json['status']?.toString() ?? '',
    totalEstimatedCost: parseMoney(json['totalEstimatedCost']), createdAt: parseTimestamp(json['createdAt']),
    updatedAt: parseTimestamp(json['updatedAt']), dayCount: (json['dayCount'] as num?)?.toInt() ?? 0);
}

class ItineraryDay {
  const ItineraryDay({required this.id, required this.dayNumber, required this.date, required this.items});

  final String id;
  final int dayNumber;
  final DateTime date;
  final List<ItineraryItem> items;

  factory ItineraryDay.fromJson(Map<String, dynamic> json) => ItineraryDay(
    id: json['id']?.toString() ?? '',
    dayNumber: (json['dayNumber'] as num?)?.toInt() ?? 0,
    date: parseCalendarDate(json['date']),
    items: (json['items'] as List<dynamic>? ?? const [])
        .whereType<Map>()
        .map((item) => ItineraryItem.fromJson(Map<String, dynamic>.from(item)))
        .toList(),
  );
}

class ItineraryItem {
  const ItineraryItem({required this.id, required this.attractionId, required this.startTime, required this.endTime, required this.estimatedCost, this.notes});

  final String id;
  final String attractionId;
  final String startTime;
  final String endTime;
  final double estimatedCost;
  final String? notes;

  factory ItineraryItem.fromJson(Map<String, dynamic> json) => ItineraryItem(
    id: json['id']?.toString() ?? '',
    attractionId: json['attractionId']?.toString() ?? '',
    startTime: cleanTime(json['startTime']),
    endTime: cleanTime(json['endTime']),
    estimatedCost: parseMoney(json['estimatedCost']),
    notes: json['notes'] as String?,
  );
}

DateTime parseCalendarDate(Object? value) {
  final text = value?.toString() ?? '';
  final match = RegExp(r'^(\d{4})-(\d{2})-(\d{2})').firstMatch(text);
  if (match == null) return DateTime(1970);
  return DateTime(int.parse(match.group(1)!), int.parse(match.group(2)!), int.parse(match.group(3)!));
}

DateTime? parseTimestamp(Object? value) => DateTime.tryParse(value?.toString() ?? '');

double parseMoney(Object? value) => double.tryParse(value?.toString() ?? '') ?? 0;

String cleanTime(Object? value) {
  final text = value?.toString() ?? '';
  return text.length >= 5 ? text.substring(0, 5) : text;
}
