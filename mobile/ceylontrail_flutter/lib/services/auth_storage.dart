import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../models/auth_user.dart';

abstract interface class AuthSessionStorage {
  Future<StoredSession?> read();

  Future<void> write({
    required String token,
    required DateTime expiresAt,
    required AuthUser user,
  });

  Future<void> clear();
}

class StoredSession {
  const StoredSession({
    required this.token,
    required this.expiresAt,
    required this.user,
  });

  final String token;
  final DateTime expiresAt;
  final AuthUser user;
}

class SecureAuthStorage implements AuthSessionStorage {
  SecureAuthStorage({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  static const _tokenKey = 'auth_token';
  static const _expiresAtKey = 'auth_expires_at';
  static const _userKey = 'auth_user';

  final FlutterSecureStorage _storage;

  @override
  Future<StoredSession?> read() async {
    try {
      final values = await _storage.readAll();
      final token = values[_tokenKey];
      final expiresAtValue = values[_expiresAtKey];
      final userValue = values[_userKey];
      if (token == null || expiresAtValue == null || userValue == null) {
        return null;
      }

      final expiresAt = DateTime.parse(expiresAtValue).toUtc();
      if (!expiresAt.isAfter(DateTime.now().toUtc())) {
        await clear();
        return null;
      }

      return StoredSession(
        token: token,
        expiresAt: expiresAt,
        user: AuthUser.fromJson(jsonDecode(userValue) as Map<String, dynamic>),
      );
    } catch (_) {
      await clear();
      return null;
    }
  }

  @override
  Future<void> write({
    required String token,
    required DateTime expiresAt,
    required AuthUser user,
  }) async {
    await _storage.write(key: _tokenKey, value: token);
    await _storage.write(key: _expiresAtKey, value: expiresAt.toUtc().toIso8601String());
    await _storage.write(key: _userKey, value: jsonEncode(user.toJson()));
  }

  @override
  Future<void> clear() => _storage.deleteAll();
}
