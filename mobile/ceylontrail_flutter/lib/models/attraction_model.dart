class CategoryModel {
  const CategoryModel({required this.id, required this.name, this.description});

  final String id;
  final String name;
  final String? description;

  factory CategoryModel.fromJson(Map<String, dynamic> json) => CategoryModel(
    id: json['id']?.toString() ?? '',
    name: json['name'] as String? ?? '',
    description: json['description'] as String?,
  );
}

class AttractionScheduleModel {
  const AttractionScheduleModel({
    required this.id,
    required this.attractionId,
    required this.dayOfWeek,
    this.openingTime,
    this.closingTime,
    required this.isClosed,
  });

  final String id;
  final String attractionId;
  final String dayOfWeek;
  final String? openingTime;
  final String? closingTime;
  final bool isClosed;

  factory AttractionScheduleModel.fromJson(Map<String, dynamic> json) => AttractionScheduleModel(
    id: json['id']?.toString() ?? '',
    attractionId: json['attractionId']?.toString() ?? '',
    dayOfWeek: json['dayOfWeek']?.toString() ?? '',
    openingTime: json['openingTime'] as String?,
    closingTime: json['closingTime'] as String?,
    isClosed: json['isClosed'] as bool? ?? false,
  );
}

class ExperienceSlotModel {
  const ExperienceSlotModel({
    required this.id,
    required this.attractionId,
    required this.date,
    required this.startTime,
    required this.endTime,
    required this.capacity,
    required this.availableCapacity,
  });

  final String id;
  final String attractionId;
  final DateTime date;
  final String startTime;
  final String endTime;
  final int capacity;
  final int availableCapacity;

  factory ExperienceSlotModel.fromJson(Map<String, dynamic> json) => ExperienceSlotModel(
    id: json['id']?.toString() ?? '',
    attractionId: json['attractionId']?.toString() ?? '',
    date: DateTime.tryParse(json['date']?.toString() ?? '') ?? DateTime(1970),
    startTime: json['startTime']?.toString() ?? '',
    endTime: json['endTime']?.toString() ?? '',
    capacity: (json['capacity'] as num?)?.toInt() ?? 0,
    availableCapacity: (json['availableCapacity'] as num?)?.toInt() ?? 0,
  );
}

class AttractionImageModel {
  const AttractionImageModel({
    required this.id,
    required this.attractionId,
    required this.imageUrl,
    this.altText,
    required this.sortOrder,
    required this.createdAt,
  });

  final String id;
  final String attractionId;
  final String imageUrl;
  final String? altText;
  final int sortOrder;
  final DateTime? createdAt;

  factory AttractionImageModel.fromJson(Map<String, dynamic> json) => AttractionImageModel(
    id: json['id']?.toString() ?? '',
    attractionId: json['attractionId']?.toString() ?? '',
    imageUrl: json['imageUrl'] as String? ?? '',
    altText: json['altText'] as String?,
    sortOrder: (json['sortOrder'] as num?)?.toInt() ?? 0,
    createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
  );
}

class AttractionModel {
  const AttractionModel({
    required this.id,
    required this.providerId,
    required this.categoryId,
    required this.name,
    required this.description,
    required this.district,
    required this.address,
    required this.latitude,
    required this.longitude,
    required this.price,
    required this.status,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
    required this.category,
    required this.schedules,
    required this.experienceSlots,
    required this.images,
    required this.isFavorite,
  });

  final String id;
  final String providerId;
  final String categoryId;
  final String name;
  final String description;
  final String district;
  final String address;
  final double latitude;
  final double longitude;
  final double price;
  final String status;
  final bool isActive;
  final DateTime? createdAt;
  final DateTime? updatedAt;
  final CategoryModel? category;
  final List<AttractionScheduleModel> schedules;
  final List<ExperienceSlotModel> experienceSlots;
  final List<AttractionImageModel> images;
  final bool isFavorite;

  factory AttractionModel.fromJson(Map<String, dynamic> json) => AttractionModel(
    id: json['id']?.toString() ?? '',
    providerId: json['providerId']?.toString() ?? '',
    categoryId: json['categoryId']?.toString() ?? '',
    name: json['name'] as String? ?? '',
    description: json['description'] as String? ?? '',
    district: json['district'] as String? ?? '',
    address: json['address'] as String? ?? '',
    latitude: (json['latitude'] as num?)?.toDouble() ?? 0,
    longitude: (json['longitude'] as num?)?.toDouble() ?? 0,
    price: (json['price'] as num?)?.toDouble() ?? 0,
    status: json['status']?.toString() ?? '',
    isActive: json['isActive'] as bool? ?? false,
    createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
    updatedAt: DateTime.tryParse(json['updatedAt']?.toString() ?? ''),
    category: json['category'] is Map
        ? CategoryModel.fromJson(Map<String, dynamic>.from(json['category'] as Map))
        : null,
    schedules: _list(json['schedules'], AttractionScheduleModel.fromJson),
    experienceSlots: _list(json['experienceSlots'], ExperienceSlotModel.fromJson),
    images: _list(json['images'], AttractionImageModel.fromJson),
    isFavorite: json['isFavorite'] as bool? ?? false,
  );

  static List<T> _list<T>(Object? value, T Function(Map<String, dynamic>) parse) =>
      (value as List<dynamic>? ?? const [])
          .whereType<Map>()
          .map((item) => parse(Map<String, dynamic>.from(item)))
          .toList();
}

class AttractionSearchResponse {
  const AttractionSearchResponse({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
    required this.totalPages,
  });

  final List<AttractionModel> items;
  final int totalCount;
  final int page;
  final int pageSize;
  final int totalPages;

  factory AttractionSearchResponse.fromJson(Map<String, dynamic> json) => AttractionSearchResponse(
    items: (json['items'] as List<dynamic>? ?? const [])
        .whereType<Map>()
        .map((item) => AttractionModel.fromJson(Map<String, dynamic>.from(item)))
        .toList(),
    totalCount: (json['totalCount'] as num?)?.toInt() ?? 0,
    page: (json['page'] as num?)?.toInt() ?? 1,
    pageSize: (json['pageSize'] as num?)?.toInt() ?? 20,
    totalPages: (json['totalPages'] as num?)?.toInt() ?? 0,
  );
}

class AvailabilityModel {
  const AvailabilityModel({required this.attractionId, this.date, required this.slots});

  final String attractionId;
  final DateTime? date;
  final List<ExperienceSlotModel> slots;

  factory AvailabilityModel.fromJson(Map<String, dynamic> json) => AvailabilityModel(
    attractionId: json['attractionId']?.toString() ?? '',
    date: DateTime.tryParse(json['date']?.toString() ?? ''),
    slots: (json['slots'] as List<dynamic>? ?? const [])
        .whereType<Map>()
        .map((item) => ExperienceSlotModel.fromJson(Map<String, dynamic>.from(item)))
        .toList(),
  );
}
