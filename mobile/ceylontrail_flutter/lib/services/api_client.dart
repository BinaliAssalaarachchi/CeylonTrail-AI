import 'package:dio/dio.dart';

import '../config/api_config.dart';
import 'auth_storage.dart';

class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  @override
  String toString() => message;
}

class ApiClient {
  ApiClient({required AuthSessionStorage storage, Dio? dio})
      : _storage = storage,
        _dio = dio ?? Dio(BaseOptions(baseUrl: ApiConfig.baseUrl)) {
    _dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) async {
          final session = await _storage.read();
          if (session != null) {
            options.headers['Authorization'] = 'Bearer ${session.token}';
          }
          handler.next(options);
        },
      ),
    );
  }

  final AuthSessionStorage _storage;
  final Dio _dio;

  Future<Response<dynamic>> post(String path, {Object? data}) async {
    try {
      return await _dio.post(path, data: data);
    } on DioException catch (error) {
      throw ApiException(_messageFor(error), statusCode: error.response?.statusCode);
    }
  }

  Future<Response<dynamic>> get(
    String path, {
    Map<String, dynamic>? queryParameters,
  }) async {
    try {
      return await _dio.get(path, queryParameters: queryParameters);
    } on DioException catch (error) {
      throw ApiException(_messageFor(error), statusCode: error.response?.statusCode);
    }
  }

  Future<Response<dynamic>> put(String path, {Object? data}) async {
    try {
      return await _dio.put(path, data: data);
    } on DioException catch (error) {
      throw ApiException(_messageFor(error), statusCode: error.response?.statusCode);
    }
  }

  Future<Response<dynamic>> delete(
    String path, {
    Object? data,
    Map<String, dynamic>? queryParameters,
  }) async {
    try {
      return await _dio.delete(
        path,
        data: data,
        queryParameters: queryParameters,
      );
    } on DioException catch (error) {
      throw ApiException(_messageFor(error), statusCode: error.response?.statusCode);
    }
  }


  static String _messageFor(DioException error) {
    final responseMessage = (error.response?.data as Map?)?['message'];
    if (responseMessage is String && responseMessage.isNotEmpty) {
      return responseMessage;
    }
    if (error.response?.statusCode == 401) return 'Invalid email or password.';
    if (error.response?.statusCode == 403) return 'You do not have access to this feature.';
    if (error.response?.statusCode == 404) return 'The requested item could not be found.';
    if (error.response?.statusCode == 400) return 'Please check the information and try again.';
    if (error.response?.statusCode == 503) return 'Planner service is temporarily unavailable. Please try again.';
    if (error.type == DioExceptionType.connectionError ||
        error.type == DioExceptionType.connectionTimeout ||
        error.type == DioExceptionType.receiveTimeout) {
      return 'Unable to reach CeylonTrail. Check that the API is running.';
    }
    return 'The request could not be completed. Please try again.';
  }
}
