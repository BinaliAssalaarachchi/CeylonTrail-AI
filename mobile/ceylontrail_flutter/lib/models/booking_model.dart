class BookingItemModel {
  const BookingItemModel({
    required this.id,
    required this.attractionId,
    required this.quantity,
    required this.unitPrice,
    required this.subtotal,
  });

  final String id;
  final String attractionId;
  final int quantity;
  final double unitPrice;
  final double subtotal;

  factory BookingItemModel.fromJson(Map<String, dynamic> json) =>
      BookingItemModel(
        id: json['id'] as String,
        attractionId: json['attractionId'] as String,
        quantity: json['quantity'] as int,
        unitPrice: (json['unitPrice'] as num).toDouble(),
        subtotal: (json['subtotal'] as num).toDouble(),
      );
}

class CancellationModel {
  const CancellationModel({
    required this.id,
    required this.reason,
    required this.cancelledBy,
    required this.cancelledAt,
  });

  final String id;
  final String reason;
  final String cancelledBy;
  final DateTime cancelledAt;

  factory CancellationModel.fromJson(Map<String, dynamic> json) =>
      CancellationModel(
        id: json['id'] as String,
        reason: json['reason'] as String,
        cancelledBy: json['cancelledBy'] as String,
        cancelledAt: DateTime.parse(json['cancelledAt'] as String),
      );
}

class BookingModel {
  const BookingModel({
    required this.id,
    required this.touristId,
    this.tripId,
    required this.status,
    required this.totalAmount,
    required this.createdAt,
    required this.updatedAt,
    required this.items,
    this.cancellation,
  });

  final String id;
  final String touristId;
  final String? tripId;
  final String status;
  final double totalAmount;
  final DateTime createdAt;
  final DateTime updatedAt;
  final List<BookingItemModel> items;
  final CancellationModel? cancellation;

  factory BookingModel.fromJson(Map<String, dynamic> json) => BookingModel(
        id: json['id'] as String,
        touristId: json['touristId'] as String,
        tripId: json['tripId'] as String?,
        status: json['status'] as String,
        totalAmount: (json['totalAmount'] as num).toDouble(),
        createdAt: DateTime.parse(json['createdAt'] as String),
        updatedAt: DateTime.parse(json['updatedAt'] as String),
        items: (json['items'] as List<dynamic>)
            .map((item) => BookingItemModel.fromJson(item as Map<String, dynamic>))
            .toList(),
        cancellation: json['cancellation'] != null
            ? CancellationModel.fromJson(
                json['cancellation'] as Map<String, dynamic>)
            : null,
      );
}
