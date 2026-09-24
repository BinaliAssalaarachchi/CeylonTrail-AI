import 'package:dio/dio.dart';

import '../config/api_config.dart';
import 'auth_storage.dart';

class ApiException implements Exception {
  const ApiException(this.message);

  final String message;

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
      throw ApiException(_messageFor(error));
    }
  }

  Future<Response<dynamic>> get(
    String path, {
    Map<String, dynamic>? queryParameters,
  }) async {
    try {
      return await _dio.get(path, queryParameters: queryParameters);
    } on DioException catch (error) {
      throw ApiException(_messageFor(error));
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
      throw ApiException(_messageFor(error));
    }
  }


  static String _messageFor(DioException error) {
    final responseMessage = (error.response?.data as Map?)?['message'];
    if (responseMessage is String && responseMessage.isNotEmpty) {
      return responseMessage;
    }
    if (error.response?.statusCode == 401) return 'Invalid email or password.';
    if (error.type == DioExceptionType.connectionError ||
        error.type == DioExceptionType.connectionTimeout ||
        error.type == DioExceptionType.receiveTimeout) {
      return 'Unable to reach CeylonTrail. Check that the API is running.';
    }
    return 'The request could not be completed. Please try again.';
  }
}
