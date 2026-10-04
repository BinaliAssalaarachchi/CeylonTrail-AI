import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'services/auth_service.dart';
import 'services/trip_api_service.dart';
import 'models/auth_user.dart';
import 'models/trip_model.dart';
import 'theme/app_theme.dart';
import 'views/home_page.dart';
import 'views/bookings_page.dart';
import 'views/login_page.dart';
import 'views/register_page.dart';
import 'views/welcome_page.dart';
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
    final initials = _profileInitials(user.firstName, user.lastName);

    return LayoutBuilder(
      builder: (context, constraints) {
        final isWide = constraints.maxWidth >= 760;
        final content = SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 620),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    _ProfileHero(),
                    Transform.translate(
                      offset: const Offset(0, -34),
                      child: _IdentityCard(user: user, initials: initials),
                    ),
                    const SizedBox(height: 0),
                    _ProfileSectionLabel(
                      'Your travel space',
                      color: isWide ? Colors.white : CeylonColors.forest,
                    ),
                    const SizedBox(height: 10),
                    _ShortcutCard(
                      icon: Icons.bookmark_outline,
                      title: 'Saved places',
                      description: 'Places you want to remember',
                      onTap: () => context.push('/favorites'),
                    ),
                    const SizedBox(height: 10),
                    _ShortcutCard(
                      icon: Icons.route_outlined,
                      title: 'My trips',
                      description: 'Your Sri Lankan journeys',
                      onTap: () => context.push('/trips'),
                    ),
                    const SizedBox(height: 10),
                    _ShortcutCard(
                      icon: Icons.confirmation_number_outlined,
                      title: 'My bookings',
                      description: 'Reservations and booking status',
                      onTap: () => context.push('/bookings'),
                    ),
                    const SizedBox(height: 26),
                    OutlinedButton.icon(
                      onPressed: authService.logout,
                      icon: const Icon(Icons.logout_outlined, size: 19),
                      label: const Text('Log out'),
                      style: OutlinedButton.styleFrom(
                        minimumSize: const Size.fromHeight(48),
                        backgroundColor: CeylonColors.ivory,
                        foregroundColor: CeylonColors.forest,
                        shadowColor: const Color(0x220E3B2E),
                        elevation: 2,
                        side: const BorderSide(color: Color(0xFFD8D8D1)),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(CeylonRadii.field)),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        );

        if (!isWide) return content;
        return Stack(
          fit: StackFit.expand,
          children: [
            Image.asset('assets/images/destinations/bentota/02.jpg', fit: BoxFit.cover),
            ColoredBox(color: CeylonColors.forestDeep.withValues(alpha: .56)),
            content,
          ],
        );
      },
    );
  }
}

String _profileInitials(String firstName, String lastName) {
  final first = firstName.trim().isEmpty ? '' : firstName.trim()[0];
  final last = lastName.trim().isEmpty ? '' : lastName.trim()[0];
  final initials = '$first$last'.toUpperCase();
  return initials.isEmpty ? 'CT' : initials;
}

class _ProfileHero extends StatelessWidget {
  @override
  Widget build(BuildContext context) => ClipRRect(
    borderRadius: BorderRadius.circular(28),
    child: SizedBox(
      height: 188,
      child: Stack(
        fit: StackFit.expand,
        children: [
          Image.asset('assets/images/destinations/bentota/02.jpg', fit: BoxFit.cover, semanticLabel: 'Bentota coast in Sri Lanka'),
          DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: [Colors.transparent, CeylonColors.forestDeep.withValues(alpha: .82)],
              ),
            ),
          ),
          const Positioned(top: 18, left: 18, child: _ProfileBrand()),
          const Positioned(left: 20, bottom: 18, child: Text('A place for the journeys ahead', style: TextStyle(color: Colors.white, fontSize: 15, fontWeight: FontWeight.w600))),
        ],
      ),
    ),
  );
}

class _ProfileBrand extends StatelessWidget {
  const _ProfileBrand();

  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      ColorFiltered(colorFilter: const ColorFilter.mode(Colors.white, BlendMode.srcIn), child: const BrandMark(size: 28)),
      const SizedBox(width: 7),
      const Text('CeylonTrail', style: TextStyle(color: Colors.white, fontFamily: 'Playfair Display', fontSize: 17, fontWeight: FontWeight.w600)),
    ],
  );
}

class _IdentityCard extends StatelessWidget {
  const _IdentityCard({required this.user, required this.initials});

  final AuthUser user;
  final String initials;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.fromLTRB(18, 16, 18, 17),
    decoration: BoxDecoration(
      color: CeylonColors.ivory,
      borderRadius: BorderRadius.circular(24),
      boxShadow: const [BoxShadow(color: Color(0x180E3B2E), blurRadius: 18, offset: Offset(0, 8))],
    ),
    child: Row(
      children: [
        CircleAvatar(radius: 29, backgroundColor: CeylonColors.mint, child: Text(initials, style: const TextStyle(color: CeylonColors.forest, fontWeight: FontWeight.w800, fontSize: 17))),
        const SizedBox(width: 14),
        Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text('Traveller', style: Theme.of(context).textTheme.labelSmall?.copyWith(color: CeylonColors.tea, fontWeight: FontWeight.w800, letterSpacing: 1.1)), const SizedBox(height: 3), Text(user.displayName, style: Theme.of(context).textTheme.titleLarge), const SizedBox(height: 2), Text(user.email.trim(), maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.bodySmall)])),
      ],
    ),
  );
}

class _ProfileSectionLabel extends StatelessWidget {
  const _ProfileSectionLabel(this.label, {required this.color});
  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) => Text(
    label,
    style: Theme.of(context).textTheme.titleMedium?.copyWith(
      color: color,
      fontSize: 18,
    ),
  );
}

class _ShortcutCard extends StatelessWidget {
  const _ShortcutCard({required this.icon, required this.title, required this.description, required this.onTap});
  final IconData icon;
  final String title;
  final String description;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Material(
    color: CeylonColors.ivory,
    borderRadius: BorderRadius.circular(18),
    child: InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(18),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 15, vertical: 14),
        child: Row(children: [Container(padding: const EdgeInsets.all(10), decoration: BoxDecoration(color: CeylonColors.mint, borderRadius: BorderRadius.circular(13)), child: Icon(icon, color: CeylonColors.forest, size: 21)), const SizedBox(width: 13), Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(title, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontSize: 15)), const SizedBox(height: 2), Text(description, style: Theme.of(context).textTheme.bodySmall)])), const Icon(Icons.chevron_right, color: CeylonColors.inkMuted)]),
      ),
    ),
  );
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
    initialLocation: '/welcome',
    refreshListenable: authService,
    redirect: (context, state) {
      final isLogin = state.matchedLocation == '/login';
      final isAuthEntry = isLogin ||
          state.matchedLocation == '/register' ||
          state.matchedLocation == '/welcome';
      if (authService.status == AuthStatus.restoring) return null;
      if (!authService.isAuthenticated && !isAuthEntry) return '/welcome';
      if (authService.isAuthenticated && isAuthEntry) return '/';
      if (state.matchedLocation.startsWith('/trips') &&
          authService.user?.role != 'Tourist')
        return '/';
      return null;
    },
    routes: [
      GoRoute(path: '/welcome', builder: (context, state) => const WelcomePage()),
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
      debugShowCheckedModeBanner: false,
      routerConfig: _router,
    ),
  );
}
