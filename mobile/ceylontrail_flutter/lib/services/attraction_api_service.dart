import '../models/attraction_model.dart';
import 'api_client.dart';

class AttractionApiService {
  const AttractionApiService(this._client);

  final ApiClient _client;

  Future<AttractionSearchResponse> searchAttractions({
    String? keyword,
    String? district,
    String? categoryId,
    double? minPrice,
    double? maxPrice,
    DateTime? date,
    String sort = 'name_asc',
    int page = 1,
    int pageSize = 10,
  }) async {
    final response = await _client.get(
      '/api/attractions/search',
      queryParameters: {
        if (keyword != null && keyword.trim().isNotEmpty) 'keyword': keyword.trim(),
        if (district != null && district.trim().isNotEmpty) 'district': district.trim(),
        if (categoryId != null && categoryId.isNotEmpty) 'categoryId': categoryId,
        if (minPrice != null) 'minPrice': minPrice,
        if (maxPrice != null) 'maxPrice': maxPrice,
        if (date != null) 'date': _dateValue(date),
        'sort': sort,
        'page': page,
        'pageSize': pageSize,
      },
    );
    final data = response.data;
    if (data is! Map) return const AttractionSearchResponse(items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0);
    return AttractionSearchResponse.fromJson(Map<String, dynamic>.from(data));
  }

  Future<List<CategoryModel>> getCategories() async {
    final response = await _client.get('/api/attractions/categories');
    final data = response.data;
    if (data is! List) return const [];
    return data
        .whereType<Map>()
        .map((item) => CategoryModel.fromJson(Map<String, dynamic>.from(item)))
        .toList();
  }

  Future<AttractionModel> getAttraction(String id) async {
    final response = await _client.get('/api/attractions/$id');
    return AttractionModel.fromJson(Map<String, dynamic>.from(response.data as Map));
  }

  Future<AvailabilityModel> getAvailability(String id, {DateTime? date}) async {
    final response = await _client.get(
      '/api/attractions/$id/availability',
      queryParameters: date == null ? null : {'date': _dateValue(date)},
    );
    return AvailabilityModel.fromJson(Map<String, dynamic>.from(response.data as Map));
  }

  Future<AttractionSearchResponse> getFavorites({int page = 1, int pageSize = 20}) async {
    final response = await _client.get(
      '/api/attractions/favorites',
      queryParameters: {'page': page, 'pageSize': pageSize},
    );
    final data = response.data;
    if (data is! Map) return const AttractionSearchResponse(items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0);
    return AttractionSearchResponse.fromJson(Map<String, dynamic>.from(data));
  }

  Future<void> addFavorite(String id) => _client.post('/api/attractions/$id/favorite');

  Future<void> removeFavorite(String id) => _client.delete('/api/attractions/$id/favorite');

  static String _dateValue(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
}
