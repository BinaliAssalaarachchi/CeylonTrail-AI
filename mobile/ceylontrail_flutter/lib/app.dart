import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'services/auth_service.dart';
import 'services/trip_api_service.dart';
import 'models/trip_model.dart';
import 'theme/app_theme.dart';
import 'views/home_page.dart';
import 'views/bookings_page.dart';
import 'views/login_page.dart';
import 'views/register_page.dart';
import 'views/discover_page.dart';
import 'views/attraction_detail_page.dart';
import 'views/booking_page.dart';
import 'views/favorites_page.dart';
import 'views/travel_alerts_page.dart';
import 'views/my_trips_page.dart';
import 'views/trip_details_page.dart';
import 'views/travel_safety_page.dart';
import 'views/trip_form_page.dart';
import 'views/trip_preferences_page.dart';
import 'views/itinerary_page.dart';
import 'views/itinerary_day_page.dart';
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
  CeylonTrailApp({required this.authService, this.tripApiService, super.key})
    : _router = _createRouter(authService, tripApiService);

  final AuthService authService;
  final TripApiService? tripApiService;
  final GoRouter _router;

  static GoRouter _createRouter(
    AuthService authService,
    TripApiService? tripApiService,
  ) => GoRouter(
    initialLocation: '/login',
    refreshListenable: authService,
    redirect: (context, state) {
      final isLogin = state.matchedLocation == '/login';
      final isAuthEntry = isLogin || state.matchedLocation == '/register';
      if (authService.status == AuthStatus.restoring) return null;
      if (!authService.isAuthenticated && !isAuthEntry) return '/login';
      if (authService.isAuthenticated && isAuthEntry) return '/';
      if (state.matchedLocation.startsWith('/trips') &&
          authService.user?.role != 'Tourist')
        return '/';
      return null;
    },
    routes: [
      GoRoute(path: '/login', builder: (context, state) => const LoginPage()),
      GoRoute(path: '/register', builder: (context, state) => const RegisterPage()),
      GoRoute(
        path: '/favorites',
        builder: (context, state) => const FavoritesPage(),
      ),
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
                    builder: (context, state) =>
                        TravelAlertDetailPage(id: state.pathParameters['id']!),
                  ),
                ],
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/discover',
                builder: (context, state) => const DiscoverPage(),
                routes: [
                  GoRoute(
                    path: ':id',
                    builder: (context, state) =>
                        AttractionDetailPage(id: state.pathParameters['id']!),
                    routes: [
                      GoRoute(
                        path: 'book',
                        builder: (context, state) => BookingPage(
                          attractionId: state.pathParameters['id']!,
                          tripApi: tripApiService!,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/trips',
                builder: (context, state) => MyTripsPage(api: tripApiService!),
                routes: [
                  GoRoute(
                    path: 'create',
                    builder: (context, state) =>
                        TripFormPage(api: tripApiService!),
                  ),
                  GoRoute(
                    path: ':id',
                    builder: (context, state) => TripDetailsPage(
                      api: tripApiService!,
                      id: state.pathParameters['id']!,
                    ),
                    routes: [
                      GoRoute(
                        path: 'edit',
                        builder: (context, state) => TripFormPage(
                          api: tripApiService!,
                          trip: state.extra as Trip?,
                        ),
                      ),
                      GoRoute(
                        path: 'preferences',
                        builder: (context, state) => TripPreferencesPage(
                          api: tripApiService!,
                          tripId: state.pathParameters['id']!,
                        ),
                      ),
                      GoRoute(
                        path: 'travel-safety',
                        builder: (context, state) => TravelSafetyPage(
                          tripId: state.pathParameters['id']!,
                        ),
                      ),
                      GoRoute(
                        path: 'itinerary',
                        builder: (context, state) => ItineraryPage(
                          api: tripApiService!,
                          tripId: state.pathParameters['id']!,
                        ),
                        routes: [
                          GoRoute(
                            path: 'day/:dayNumber',
                            builder: (context, state) => ItineraryDayPage(
                              day: state.extra! as ItineraryDay,
                            ),
                          ),
                        ],
                      ),
                    ],
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
