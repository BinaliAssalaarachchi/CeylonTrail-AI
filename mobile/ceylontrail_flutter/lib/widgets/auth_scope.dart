import 'package:flutter/widgets.dart';

import '../services/auth_service.dart';

class AuthScope extends InheritedNotifier<AuthService> {
  const AuthScope({required super.notifier, required super.child, super.key});

  static AuthService of(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<AuthScope>()!.notifier!;
}
