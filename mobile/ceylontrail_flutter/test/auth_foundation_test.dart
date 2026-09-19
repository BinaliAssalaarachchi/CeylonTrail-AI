import 'package:flutter_test/flutter_test.dart';

import 'package:ceylontrail_flutter/models/auth_response.dart';
import 'package:ceylontrail_flutter/models/auth_user.dart';
import 'package:ceylontrail_flutter/services/auth_api_service.dart';
import 'package:ceylontrail_flutter/services/auth_service.dart';
import 'package:ceylontrail_flutter/services/auth_storage.dart';

class FakeAuthApi implements AuthApi {
  FakeAuthApi(this.response);

  final AuthResponse response;
  int calls = 0;

  @override
  Future<AuthResponse> login({required String email, required String password}) async {
    calls++;
    return response;
  }
}

class FakeAuthStorage implements AuthSessionStorage {
  StoredSession? session;

  @override
  Future<void> clear() async => session = null;

  @override
  Future<StoredSession?> read() async => session;

  @override
  Future<void> write({required String token, required DateTime expiresAt, required AuthUser user}) async {
    session = StoredSession(token: token, expiresAt: expiresAt, user: user);
  }
}

void main() {
  final user = AuthUser(
    id: 'user-id',
    firstName: 'Nimal',
    lastName: 'Perera',
    email: 'nimal@example.com',
    role: 'Tourist',
    isActive: true,
  );

  test('AuthResponse parses the backend login contract', () {
    final response = AuthResponse.fromJson({
      'token': 'jwt-token',
      'expiresAt': '2030-01-01T12:00:00Z',
      'user': user.toJson(),
    });

    expect(response.token, 'jwt-token');
    expect(response.user.displayName, 'Nimal Perera');
    expect(response.user.role, 'Tourist');
  });

  test('AuthService persists a safe session and logs out', () async {
    final storage = FakeAuthStorage();
    final api = FakeAuthApi(AuthResponse(
      token: 'jwt-token',
      expiresAt: DateTime.utc(2030),
      user: user,
    ));
    final service = AuthService(api: api, storage: storage);

    expect(await service.login(email: user.email, password: 'not-persisted'), isTrue);
    expect(service.isAuthenticated, isTrue);
    expect(storage.session?.token, 'jwt-token');
    expect(api.calls, 1);

    await service.logout();
    expect(service.isAuthenticated, isFalse);
    expect(storage.session, isNull);
  });
}
