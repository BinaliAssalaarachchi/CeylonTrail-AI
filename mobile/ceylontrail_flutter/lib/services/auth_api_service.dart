import '../models/auth_response.dart';
import 'api_client.dart';

abstract interface class AuthApi {
  Future<AuthResponse> login({required String email, required String password});

  Future<AuthResponse> register({
    required String firstName,
    required String lastName,
    required String email,
    required String password,
  });
}

class AuthApiService implements AuthApi {
  const AuthApiService(this._client);

  final ApiClient _client;

  @override
  Future<AuthResponse> login({required String email, required String password}) async {
    final response = await _client.post(
      '/api/auth/login',
      data: {'email': email, 'password': password},
    );
    return AuthResponse.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<AuthResponse> register({
    required String firstName,
    required String lastName,
    required String email,
    required String password,
  }) async {
    final response = await _client.post(
      '/api/auth/register',
      data: {
        'firstName': firstName,
        'lastName': lastName,
        'email': email,
        'password': password,
        'role': 'Tourist',
      },
    );
    return AuthResponse.fromJson(response.data as Map<String, dynamic>);
  }
}
