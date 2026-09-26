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
    id: json['id']?.toString() ?? '', attractionId: json['attractionId']?.toString() ?? '',
    startTime: DateTime.tryParse(json['startTime']?.toString() ?? '') ?? DateTime(1970), endTime: DateTime.tryParse(json['endTime']?.toString() ?? '') ?? DateTime(1970),
    maxCapacity: (json['maxCapacity'] as num?)?.toInt() ?? 0, bookedCapacity: (json['bookedCapacity'] as num?)?.toInt() ?? 0,
    availableCapacity: (json['availableCapacity'] as num?)?.toInt() ?? 0, pricePerPerson: (json['pricePerPerson'] as num?)?.toDouble() ?? 0,
  );
}
 
class BookingItemModel {
  const BookingItemModel({required this.id, required this.availabilitySlotId, required this.numberOfGuests, required this.unitPrice, required this.subtotal});
  final String id;
  final String availabilitySlotId;
  final int numberOfGuests;
  final double unitPrice;
  final double subtotal;
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
  const BookingModel({required this.id, required this.userId, this.tripId, required this.currentStatus, required this.totalAmount, this.qrCodeHash, required this.createdAt, required this.updatedAt, required this.items, required this.statusHistory, required this.cancellationRequests});
  final String id;
  final String userId;
  final String? tripId;
  final String currentStatus;
  final double totalAmount;
  final String? qrCodeHash;
  final DateTime createdAt;
  final DateTime updatedAt;
  final List<BookingItemModel> items;
  final List<BookingHistoryModel> statusHistory;
  final List<CancellationModel> cancellationRequests;

  String get status => currentStatus;
  CancellationModel? get cancellation => cancellationRequests.isEmpty ? null : cancellationRequests.last;

  factory BookingModel.fromJson(Map<String, dynamic> json) => BookingModel(
    id: json['id']?.toString() ?? '', userId: (json['userId'] ?? json['touristId'])?.toString() ?? '', tripId: json['tripId']?.toString(),
    currentStatus: (json['currentStatus'] ?? json['status'])?.toString() ?? '', totalAmount: (json['totalAmount'] as num?)?.toDouble() ?? 0,
    qrCodeHash: json['qrCodeHash'] as String?, createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? '') ?? DateTime(1970), updatedAt: DateTime.tryParse(json['updatedAt']?.toString() ?? '') ?? DateTime(1970),
    items: (json['items'] as List<dynamic>? ?? const []).whereType<Map<String, dynamic>>().map((item) => BookingItemModel(
      id: item['id']?.toString() ?? '', availabilitySlotId: (item['availabilitySlotId'] ?? item['attractionId'])?.toString() ?? '', numberOfGuests: ((item['numberOfGuests'] ?? item['quantity']) as num?)?.toInt() ?? 0,
      unitPrice: (item['unitPrice'] as num?)?.toDouble() ?? 0, subtotal: ((item['subTotal'] ?? item['subtotal']) as num?)?.toDouble() ?? 0,
    )).toList(),
    statusHistory: (json['statusHistory'] as List<dynamic>? ?? const []).whereType<Map<String, dynamic>>().map((entry) => BookingHistoryModel(
      id: entry['id']?.toString() ?? '', previousStatus: entry['previousStatus']?.toString() ?? '', newStatus: entry['newStatus']?.toString() ?? '', changedByUserId: entry['changedByUserId']?.toString(), timestamp: DateTime.tryParse(entry['timestamp']?.toString() ?? '') ?? DateTime(1970), reason: entry['reason'] as String?,
    )).toList(),
    cancellationRequests: (json['cancellationRequests'] as List<dynamic>? ?? const []).whereType<Map<String, dynamic>>().map((entry) => CancellationModel(
      id: entry['id']?.toString() ?? '', reason: entry['reason']?.toString() ?? '', status: entry['status']?.toString() ?? '', refundAmount: (entry['refundAmount'] as num?)?.toDouble(), requestedAt: DateTime.tryParse(entry['requestedAt']?.toString() ?? '') ?? DateTime(1970),
    )).toList(),
  );
}
