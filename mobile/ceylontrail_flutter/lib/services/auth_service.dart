import 'package:flutter/foundation.dart';

import '../models/auth_user.dart';
import 'auth_api_service.dart';
import 'auth_storage.dart';

enum AuthStatus { restoring, unauthenticated, authenticated }

class AuthService extends ChangeNotifier {
  AuthService({required AuthApi api, required AuthSessionStorage storage})
      : _api = api,
        _storage = storage;

  final AuthApi _api;
  final AuthSessionStorage _storage;
  AuthStatus _status = AuthStatus.restoring;
  AuthUser? _user;
  String? _error;

  AuthStatus get status => _status;
  AuthUser? get user => _user;
  String? get error => _error;
  bool get isAuthenticated => _status == AuthStatus.authenticated;

  Future<void> restore() async {
    _status = AuthStatus.restoring;
    _error = null;
    notifyListeners();
    final session = await _storage.read();
    _user = session?.user;
    _status = session == null ? AuthStatus.unauthenticated : AuthStatus.authenticated;
    notifyListeners();
  }

  Future<bool> login({required String email, required String password}) async {
    _error = null;
    _status = AuthStatus.restoring;
    notifyListeners();
    try {
      final response = await _api.login(email: email.trim(), password: password);
      await _storage.write(
        token: response.token,
        expiresAt: response.expiresAt,
        user: response.user,
      );
      _user = response.user;
      _status = AuthStatus.authenticated;
      notifyListeners();
      return true;
    } catch (error) {
      _error = error.toString();
      _status = AuthStatus.unauthenticated;
      notifyListeners();
      return false;
    }
  }

  Future<void> logout() async {
    await _storage.clear();
    _user = null;
    _error = null;
    _status = AuthStatus.unauthenticated;
    notifyListeners();
  }
}
