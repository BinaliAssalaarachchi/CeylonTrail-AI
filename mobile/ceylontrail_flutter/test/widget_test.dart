import 'package:flutter_test/flutter_test.dart';

import 'package:ceylontrail_flutter/app.dart';
import 'package:ceylontrail_flutter/models/auth_response.dart';
import 'package:ceylontrail_flutter/models/auth_user.dart';
import 'package:ceylontrail_flutter/services/auth_api_service.dart';
import 'package:ceylontrail_flutter/services/auth_service.dart';
import 'package:ceylontrail_flutter/services/auth_storage.dart';

class WidgetFakeApi implements AuthApi {
  @override
  Future<AuthResponse> login({required String email, required String password}) async => AuthResponse(
        token: 'token',
        expiresAt: DateTime.utc(2030),
        user: const AuthUser(
          id: 'id',
          firstName: 'Nimal',
          lastName: 'Perera',
          email: 'nimal@example.com',
          role: 'Tourist',
          isActive: true,
        ),
      );

  @override
  Future<AuthResponse> register({required String firstName, required String lastName, required String email, required String password}) => login(email: email, password: password);
}

class WidgetFakeStorage implements AuthSessionStorage {
  @override
  Future<void> clear() async {}

  @override
  Future<StoredSession?> read() async => null;

  @override
  Future<void> write({required String token, required DateTime expiresAt, required AuthUser user}) async {}
}

void main() {
  testWidgets('shows the shared CeylonTrail login screen', (tester) async {
    final authService = AuthService(api: WidgetFakeApi(), storage: WidgetFakeStorage());
    await authService.restore();

    await tester.pumpWidget(CeylonTrailApp(authService: authService));
    await tester.pumpAndSettle();

    expect(find.text('CeylonTrail AI'), findsOneWidget);
    expect(find.text('Sign in'), findsOneWidget);
    expect(find.text('Email'), findsOneWidget);
    expect(find.text('Password'), findsOneWidget);
  });
}
