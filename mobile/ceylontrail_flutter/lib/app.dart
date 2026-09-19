import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'services/auth_service.dart';
import 'theme/app_theme.dart';
import 'views/home_page.dart';
import 'views/login_page.dart';
import 'widgets/auth_scope.dart';

class CeylonTrailApp extends StatelessWidget {
  CeylonTrailApp({required this.authService, super.key})
      : _router = _createRouter(authService);

  final AuthService authService;
  final GoRouter _router;

  static GoRouter _createRouter(AuthService authService) => GoRouter(
        initialLocation: '/login',
        refreshListenable: authService,
        redirect: (context, state) {
          final isLogin = state.matchedLocation == '/login';
          if (authService.status == AuthStatus.restoring) return null;
          if (!authService.isAuthenticated && !isLogin) return '/login';
          if (authService.isAuthenticated && isLogin) return '/';
          return null;
        },
        routes: [
          GoRoute(path: '/login', builder: (context, state) => const LoginPage()),
          GoRoute(path: '/', builder: (context, state) => const HomePage()),
        ],
      );

  @override
  Widget build(BuildContext context) => AuthScope(
        notifier: authService,
        child: MaterialApp.router(
          title: 'CeylonTrail AI',
          theme: AppTheme.light,
          routerConfig: _router,
        ),
      );
}
