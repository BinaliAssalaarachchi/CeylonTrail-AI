import 'auth_user.dart';

class AuthResponse {
  const AuthResponse({
    required this.token,
    required this.expiresAt,
    required this.user,
  });

  final String token;
  final DateTime expiresAt;
  final AuthUser user;

  factory AuthResponse.fromJson(Map<String, dynamic> json) => AuthResponse(
        token: json['token'] as String,
        expiresAt: DateTime.parse(json['expiresAt'] as String).toUtc(),
        user: AuthUser.fromJson(json['user'] as Map<String, dynamic>),
      );
}
