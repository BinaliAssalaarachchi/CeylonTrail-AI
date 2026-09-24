import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'services/auth_service.dart';
import 'theme/app_theme.dart';
import 'views/home_page.dart';
import 'views/bookings_page.dart';
import 'views/login_page.dart';
import 'views/module_placeholder_page.dart';
import 'views/travel_alerts_page.dart';
import 'views/trips_page.dart';
import 'views/travel_safety_page.dart';
import 'widgets/auth_scope.dart';
import 'widgets/brand_mark.dart';
import 'widgets/mobile_shell.dart';

class ProfilePage extends StatelessWidget {
  const ProfilePage({super.key});

  @override
  Widget build(BuildContext context) {
    final authService = AuthScope.of(context);
    final user = authService.user!;
    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const BrandLockup(compact: true),
            const SizedBox(height: 44),
            Text(
              'Your profile',
              style: Theme.of(context).textTheme.displaySmall,
            ),
            const SizedBox(height: 8),
            Text(
              user.displayName,
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 4),
            Text(user.email, style: Theme.of(context).textTheme.bodyLarge),
            const SizedBox(height: 24),
            Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Row(
                  children: [
                    const Icon(
                      Icons.verified_user_outlined,
                      color: CeylonColors.tea,
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        'Your secure CeylonTrail session is active.',
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 24),
            OutlinedButton.icon(
              onPressed: authService.logout,
              icon: const Icon(Icons.logout),
              label: const Text('Log out'),
            ),
          ],
        ),
      ),
    );
  }
}

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
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) =>
            MobileShell(navigationShell: navigationShell),
        branches: [
          StatefulShellBranch(
            routes: [
              GoRoute(path: '/', builder: (context, state) => const HomePage()),
              GoRoute(
                path: '/travel-alerts',
                builder: (context, state) => const TravelAlertsPage(),
                routes: [
                  GoRoute(
                    path: ':id',
                    builder: (context, state) => TravelAlertDetailPage(
                      id: state.pathParameters['id']!,
                    ),
                  ),
                ],
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/discover',
                builder: (context, state) => const ModulePlaceholderPage(
                  title: 'Discover',
                  description:
                      'Find places, experiences and stories that make Sri Lanka feel closer.',
                  icon: Icons.explore_outlined,
                ),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/trips',
                builder: (context, state) => const TripsPage(),
                routes: [
                  GoRoute(
                    path: ':tripId',
                    builder: (context, state) => TravelSafetyPage(
                      tripId: state.pathParameters['tripId']!,
                    ),
                  ),
                ],
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/bookings',
                builder: (context, state) => const BookingsPage(),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/profile',
                builder: (context, state) => const ProfilePage(),
              ),
            ],
          ),
        ],
      ),
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
