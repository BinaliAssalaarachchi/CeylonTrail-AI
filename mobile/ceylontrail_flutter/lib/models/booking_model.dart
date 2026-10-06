import 'travel_alert_model.dart';

class AvailabilitySlotModel {
  const AvailabilitySlotModel({required this.id, required this.attractionId, required this.startTime, required this.endTime, required this.maxCapacity, required this.bookedCapacity, required this.availableCapacity, required this.pricePerPerson});
  final String id;
  final String attractionId;
  final DateTime startTime;
  final DateTime endTime;
  final int maxCapacity;
  final int bookedCapacity;
  final int availableCapacity;
  final double pricePerPerson;

  factory AvailabilitySlotModel.fromJson(Map<String, dynamic> json) => AvailabilitySlotModel(
    id: (json['id'] ?? json['Id'])?.toString() ?? '',
    attractionId: (json['attractionId'] ?? json['AttractionId'])?.toString() ?? '',
    startTime: DateTime.tryParse((json['startTime'] ?? json['StartTime'])?.toString() ?? '') ?? DateTime(1970),
    endTime: DateTime.tryParse((json['endTime'] ?? json['EndTime'])?.toString() ?? '') ?? DateTime(1970),
    maxCapacity: ((json['maxCapacity'] ?? json['MaxCapacity']) as num?)?.toInt() ?? 0,
    bookedCapacity: ((json['bookedCapacity'] ?? json['BookedCapacity']) as num?)?.toInt() ?? 0,
    availableCapacity: ((json['availableCapacity'] ?? json['AvailableCapacity']) as num?)?.toInt() ?? 0,
    pricePerPerson: ((json['pricePerPerson'] ?? json['PricePerPerson']) as num?)?.toDouble() ?? 0,
  );
}
 
class BookingItemModel {
  const BookingItemModel({
    required this.id,
    required this.availabilitySlotId,
    required this.numberOfGuests,
    required this.unitPrice,
    required this.subtotal,
    this.attractionName,
    this.district,
    this.slotStartTime,
    this.slotEndTime,
    this.attractionImageUrl,
  });
  final String id;
  final String availabilitySlotId;
  final int numberOfGuests;
  final double unitPrice;
  final double subtotal;
  final String? attractionName;
  final String? district;
  final DateTime? slotStartTime;
  final DateTime? slotEndTime;
  final String? attractionImageUrl;
}

class BookingHistoryModel {
  const BookingHistoryModel({required this.id, required this.previousStatus, required this.newStatus, required this.changedByUserId, required this.timestamp, this.reason});
  final String id;
  final String previousStatus;
  final String newStatus;
  final String? changedByUserId;
  final DateTime timestamp;
  final String? reason;
}

class CancellationModel {
  const CancellationModel({required this.id, required this.reason, required this.status, this.refundAmount, required this.requestedAt});
  final String id;
  final String reason;
  final String status;
  final double? refundAmount;
  final DateTime requestedAt;
}

class BookingModel {
  const BookingModel({
    required this.id,
    this.tripId,
    required this.currentStatus,
    required this.totalAmount,
    required this.createdAt,
    required this.updatedAt,
    required this.items,
    required this.statusHistory,
    required this.cancellationRequests,
    this.activeAdvisories = const [],
  });
  final String id;
  final String? tripId;
  final String currentStatus;
  final double totalAmount;
  final DateTime createdAt;
  final DateTime updatedAt;
  final List<BookingItemModel> items;
  final List<BookingHistoryModel> statusHistory;
  final List<CancellationModel> cancellationRequests;
  final List<TravelAlert> activeAdvisories;

  String get status => currentStatus;
  CancellationModel? get cancellation => cancellationRequests.isEmpty ? null : cancellationRequests.last;

  String get displayTitle {
    final names = items
        .map((i) => i.attractionName?.trim())
        .where((name) => name != null && name.isNotEmpty)
        .cast<String>()
        .toSet()
        .toList();
    if (names.isNotEmpty) {
      return names.join(', ');
    }
    if (items.length > 1) {
      return '${items.length} experiences reserved';
    }
    return 'Your reservation';
  }

  factory BookingModel.fromJson(Map<String, dynamic> json) => BookingModel(
    id: json['id']?.toString() ?? '', tripId: json['tripId']?.toString(),
    currentStatus: (json['currentStatus'] ?? json['status'])?.toString() ?? '', totalAmount: (json['totalAmount'] as num?)?.toDouble() ?? 0,
    createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? '') ?? DateTime(1970), updatedAt: DateTime.tryParse(json['updatedAt']?.toString() ?? '') ?? DateTime(1970),
    items: (json['items'] as List<dynamic>? ?? const []).whereType<Map>().map((item) {
      final map = Map<String, dynamic>.from(item);
      return BookingItemModel(
        id: map['id']?.toString() ?? '',
        availabilitySlotId: (map['availabilitySlotId'] ?? map['attractionId'])?.toString() ?? '',
        numberOfGuests: ((map['numberOfGuests'] ?? map['quantity']) as num?)?.toInt() ?? 0,
        unitPrice: (map['unitPrice'] as num?)?.toDouble() ?? 0,
        subtotal: ((map['subTotal'] ?? map['subtotal']) as num?)?.toDouble() ?? 0,
        attractionName: (map['attractionName'] ?? map['AttractionName'])?.toString(),
        district: (map['district'] ?? map['District'])?.toString(),
        slotStartTime: DateTime.tryParse((map['slotStartTime'] ?? map['SlotStartTime'])?.toString() ?? ''),
        slotEndTime: DateTime.tryParse((map['slotEndTime'] ?? map['SlotEndTime'])?.toString() ?? ''),
        attractionImageUrl: (map['attractionImageUrl'] ?? map['AttractionImageUrl'])?.toString(),
      );
    }).toList(),
    statusHistory: (json['statusHistory'] as List<dynamic>? ?? const []).whereType<Map>().map((entry) {
      final map = Map<String, dynamic>.from(entry);
      return BookingHistoryModel(
        id: map['id']?.toString() ?? '', previousStatus: map['previousStatus']?.toString() ?? '', newStatus: map['newStatus']?.toString() ?? '', changedByUserId: map['changedByUserId']?.toString(), timestamp: DateTime.tryParse(map['timestamp']?.toString() ?? '') ?? DateTime(1970), reason: map['reason'] as String?,
      );
    }).toList(),
    cancellationRequests: (json['cancellationRequests'] as List<dynamic>? ?? const []).whereType<Map>().map((entry) {
      final map = Map<String, dynamic>.from(entry);
      return CancellationModel(
        id: map['id']?.toString() ?? '', reason: map['reason']?.toString() ?? '', status: map['status']?.toString() ?? '', refundAmount: (map['refundAmount'] as num?)?.toDouble(), requestedAt: DateTime.tryParse(map['requestedAt']?.toString() ?? '') ?? DateTime(1970),
      );
    }).toList(),
    activeAdvisories: (json['activeAdvisories'] as List<dynamic>? ?? const []).whereType<Map>().map((entry) {
      return TravelAlert.fromJson(Map<String, dynamic>.from(entry));
    }).toList(),
  );
}
