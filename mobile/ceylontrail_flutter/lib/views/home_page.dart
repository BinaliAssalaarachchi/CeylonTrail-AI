import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/attraction_model.dart';
import '../models/trip_model.dart';
import '../services/api_client.dart';
import '../services/attraction_api_service.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/auth_scope.dart';
import '../widgets/mobile_shell.dart';
import '../utils/attraction_images.dart';
import 'trip_widgets.dart';

class _HomeCanvas extends StatelessWidget {
  const _HomeCanvas({required this.child, this.maxWidth = 720});

  final Widget child;
  final double maxWidth;

  @override
  Widget build(BuildContext context) {
    return Container(
      color: CeylonColors.canvas,
      alignment: Alignment.topCenter,
      child: SafeArea(
        child: ConstrainedBox(
          constraints: BoxConstraints(maxWidth: maxWidth),
          child: child,
        ),
      ),
    );
  }
}

class _HomeSectionHeader extends StatelessWidget {
  const _HomeSectionHeader({required this.title, this.action, this.onTap});

  final String title;
  final String? action;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(
          title,
          style: Theme.of(context).textTheme.titleLarge?.copyWith(fontSize: 19),
        ),
        if (action != null)
          TextButton(onPressed: onTap, child: Text(action!)),
      ],
    );
  }
}

class _HomeStatusChip extends StatelessWidget {
  const _HomeStatusChip({required this.status});

  final String status;

  @override
  Widget build(BuildContext context) {
    final isPositive = {'Confirmed', 'Completed', 'Planned'}.contains(status);
    final color = isPositive ? CeylonColors.tea : CeylonColors.amber;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: .14),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        status.toUpperCase(),
        style: TextStyle(
          color: color,
          fontSize: 9,
          fontWeight: FontWeight.w800,
          letterSpacing: .5,
        ),
      ),
    );
  }
}

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  AttractionApiService? _attractions;
  TripApiService? _trips;
  List<AttractionModel> _discoveries = const [];
  List<AttractionModel> _favorites = const [];
  List<Trip> _tripList = const [];
  bool _loading = true;
  String? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_attractions != null) return;
    final client = ApiClient(storage: AuthScope.of(context).storage);
    _attractions = AttractionApiService(client);
    _trips = TripApiService(client);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        _attractions!.searchAttractions(pageSize: 8, sort: 'newest'),
        _attractions!.getFavorites(pageSize: 8),
        _trips!.getTrips(),
      ]);
      if (!mounted) return;
      setState(() {
        _discoveries = (results[0] as AttractionSearchResponse).items;
        _favorites = (results[1] as AttractionSearchResponse).items;
        _tripList = results[2] as List<Trip>;
      });
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Some travel content is unavailable right now.');
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _toggleFavorite(AttractionModel attraction) async {
    try {
      if (attraction.isFavorite) {
        await _attractions!.removeFavorite(attraction.id);
      } else {
        await _attractions!.addFavorite(attraction.id);
      }
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('That favorite could not be updated.')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final user = AuthScope.of(context).user!;
    final firstName = user.firstName.trim().isEmpty
        ? 'traveller'
        : user.firstName.trim();
    final attractions = _discoveries.isNotEmpty ? _discoveries : _favorites;
    final trip = _upcomingTrip(_tripList);

    return _HomeCanvas(
      maxWidth: 720,
      child: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 32),
          children: [
            MobileHeader(
              userName: user.displayName,
              onProfileTap: () => context.go('/profile'),
            ),
            const SizedBox(height: 22),
            Text(
              'Ayubowan, $firstName 👋',
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: CeylonColors.tea,
                fontSize: 16,
              ),
            ),
            const SizedBox(height: 5),
            Text(
              'Where will Sri Lanka take you next?',
              style: Theme.of(context).textTheme.displaySmall?.copyWith(
                fontSize: 30,
                height: 1.08,
              ),
            ),
            const SizedBox(height: 18),
            _SearchField(onTap: () => context.go('/discover')),
            const SizedBox(height: 14),
            _CategoryChips(
              categories: _categories(attractions),
              onTap: () => context.go('/discover'),
            ),
            const SizedBox(height: 28),
            _HomeSectionHeader(
              title: 'Discover Sri Lanka',
              action: 'See all',
              onTap: () => context.go('/discover'),
            ),
            const SizedBox(height: 12),
            if (_loading)
              const SizedBox(
                height: 242,
                child: Center(child: CircularProgressIndicator()),
              )
            else if (attractions.isEmpty)
              _EmptyPlaces(onTap: () => context.go('/discover'))
            else
              _AttractionRail(
                attractions: attractions.take(4).toList(),
                onFavorite: _toggleFavorite,
              ),
            const SizedBox(height: 28),
            const _HomeSectionHeader(title: 'Your next journey'),
            const SizedBox(height: 12),
            if (trip == null)
              _EmptyJourney(onTap: () => context.go('/trips/create'))
            else
              _JourneyCard(
                trip: trip,
                onTap: () => context.push('/trips/${trip.id}'),
              ),
            const SizedBox(height: 16),
            const _AiPlannerCard(),
            const SizedBox(height: 16),
            const _HomeLink(
              icon: Icons.bookmark_border_rounded,
              title: 'My bookings',
              subtitle: 'View reservations and booking history.',
              route: '/bookings',
            ),
            const SizedBox(height: 10),
            const _HomeLink(
              icon: Icons.campaign_outlined,
              title: 'Travel advisories',
              subtitle: 'Check current alerts before you set out.',
              route: '/travel-alerts',
            ),
            if (_error != null) ...[
              const SizedBox(height: 14),
              Text(_error!, style: const TextStyle(color: CeylonColors.error)),
            ],
          ],
        ),
      ),
    );
  }

  List<String> _categories(List<AttractionModel> attractions) {
    final categories = <String>{'All'};
    for (final attraction in attractions) {
      final category = attraction.category?.name.trim();
      if (category != null && category.isNotEmpty) categories.add(category);
    }
    return categories.take(5).toList();
  }
}

class _SearchField extends StatelessWidget {
  const _SearchField({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: CeylonColors.ivory,
      borderRadius: BorderRadius.circular(18),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(18),
        child: Container(
          height: 54,
          padding: const EdgeInsets.symmetric(horizontal: 16),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: const Color(0xFFE2DED3)),
          ),
          child: const Row(
            children: [
              Icon(Icons.search_rounded, color: CeylonColors.tea),
              SizedBox(width: 10),
              Expanded(
                child: Text(
                  'Search destinations and experiences',
                  style: TextStyle(color: CeylonColors.inkMuted, fontSize: 13),
                ),
              ),
              Icon(Icons.tune_rounded, size: 19, color: CeylonColors.tea),
            ],
          ),
        ),
      ),
    );
  }
}

class _CategoryChips extends StatelessWidget {
  const _CategoryChips({required this.categories, required this.onTap});

  final List<String> categories;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: categories
            .map(
              (category) => Padding(
                padding: const EdgeInsets.only(right: 8),
                child: ActionChip(
                  onPressed: onTap,
                  label: Text(category),
                  side: BorderSide.none,
                  backgroundColor: category == 'All'
                      ? CeylonColors.forest
                      : CeylonColors.ivory,
                  labelStyle: TextStyle(
                    color: category == 'All'
                        ? Colors.white
                        : CeylonColors.forest,
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            )
            .toList(),
      ),
    );
  }
}

class _AttractionRail extends StatelessWidget {
  const _AttractionRail({required this.attractions, required this.onFavorite});

  final List<AttractionModel> attractions;
  final ValueChanged<AttractionModel> onFavorite;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 242,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: attractions.length,
        separatorBuilder: (_, __) => const SizedBox(width: 12),
        itemBuilder: (context, index) => _AttractionCard(
          attraction: attractions[index],
          onFavorite: () => onFavorite(attractions[index]),
        ),
      ),
    );
  }
}

class _AttractionCard extends StatelessWidget {
  const _AttractionCard({
    required this.attraction,
    required this.onFavorite,
  });

  final AttractionModel attraction;
  final VoidCallback onFavorite;

  @override
  Widget build(BuildContext context) {
    final gallery = attractionGallery(attraction);
    final image = gallery.isEmpty ? null : gallery.first;
    return Material(
      color: CeylonColors.forestDeep,
      borderRadius: BorderRadius.circular(22),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push('/discover/${attraction.id}'),
        child: SizedBox(
          width: 166,
          child: Stack(
            fit: StackFit.expand,
            children: [
              image == null
                  ? _MissingDestinationImage(attraction: attraction)
                  : image.startsWith('http')
                      ? Image.network(
                          image,
                          fit: BoxFit.cover,
                          errorBuilder: (_, __, ___) =>
                              _MissingDestinationImage(attraction: attraction),
                        )
                      : Image.asset(
                          image,
                          fit: BoxFit.cover,
                          errorBuilder: (_, __, ___) =>
                              _MissingDestinationImage(attraction: attraction),
                        ),
              const DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: [Colors.transparent, Color(0xD900241A)],
                  ),
                ),
              ),
              Positioned(
                top: 11,
                right: 11,
                child: Material(
                  color: Colors.white.withValues(alpha: .9),
                  shape: const CircleBorder(),
                  child: InkWell(
                    onTap: onFavorite,
                    customBorder: const CircleBorder(),
                    child: Padding(
                      padding: const EdgeInsets.all(8),
                      child: Icon(
                        attraction.isFavorite
                            ? Icons.favorite
                            : Icons.favorite_border,
                        color: attraction.isFavorite
                            ? CeylonColors.error
                            : CeylonColors.forest,
                        size: 17,
                      ),
                    ),
                  ),
                ),
              ),
              Positioned(
                left: 14,
                right: 12,
                bottom: 14,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      attraction.name,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 17,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 3),
                    Text(
                      attraction.category?.name ?? attraction.district,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(color: Colors.white70, fontSize: 11),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _JourneyCard extends StatelessWidget {
  const _JourneyCard({required this.trip, required this.onTap});

  final Trip trip;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: CeylonColors.ivory,
      borderRadius: BorderRadius.circular(22),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Row(
          children: [
            SizedBox(
              width: 120,
              height: 116,
              child: _JourneyImage(tripName: trip.name),
            ),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(15, 13, 12, 13),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      trip.name,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 6),
                    Text(
                      '${displayDate(trip.startDate)}  ·  ${displayDate(trip.endDate)}',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                    const SizedBox(height: 9),
                    Row(
                      children: [
                        _HomeStatusChip(status: trip.status),
                        const Spacer(),
                        const Icon(
                          Icons.arrow_forward_rounded,
                          size: 19,
                          color: CeylonColors.tea,
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _EmptyJourney extends StatelessWidget {
  const _EmptyJourney({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: CeylonColors.ivory,
        borderRadius: BorderRadius.circular(22),
      ),
      child: Row(
        children: [
          const Expanded(
            child: Text(
              'Your next story starts here.',
              style: TextStyle(fontWeight: FontWeight.w700),
            ),
          ),
          TextButton(onPressed: onTap, child: const Text('Plan a trip')),
        ],
      ),
    );
  }
}

class _AiPlannerCard extends StatelessWidget {
  const _AiPlannerCard();

  @override
  Widget build(BuildContext context) {
    return Material(
      color: CeylonColors.forest,
      borderRadius: BorderRadius.circular(24),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.go('/trips/create'),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(18, 17, 14, 17),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(11),
                decoration: BoxDecoration(
                  color: CeylonColors.mint.withValues(alpha: .18),
                  shape: BoxShape.circle,
                ),
                child: const Icon(Icons.auto_awesome, color: CeylonColors.mint),
              ),
              const SizedBox(width: 13),
              const Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Plan with CeylonTrail AI',
                      style: TextStyle(
                        color: Colors.white,
                        fontSize: 17,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    SizedBox(height: 4),
                    Text(
                      'Turn an idea into a thoughtful Sri Lankan journey.',
                      style: TextStyle(color: Colors.white70, fontSize: 12),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.arrow_forward_rounded, color: CeylonColors.mint),
            ],
          ),
        ),
      ),
    );
  }
}

class _HomeLink extends StatelessWidget {
  const _HomeLink({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.route,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final String route;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: CeylonColors.ivory,
      borderRadius: BorderRadius.circular(18),
      child: InkWell(
        onTap: () => context.push(route),
        borderRadius: BorderRadius.circular(18),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          child: Row(
            children: [
              Icon(icon, color: CeylonColors.tea),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: const TextStyle(
                        color: CeylonColors.forest,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(subtitle, style: const TextStyle(fontSize: 12, color: CeylonColors.inkMuted)),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right, color: CeylonColors.inkMuted),
            ],
          ),
        ),
      ),
    );
  }
}

class _EmptyPlaces extends StatelessWidget {
  const _EmptyPlaces({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: CeylonColors.ivory,
        borderRadius: BorderRadius.circular(22),
      ),
      child: Row(
        children: [
          const Expanded(child: Text('Explore places worth remembering.')),
          TextButton(onPressed: onTap, child: const Text('Discover')),
        ],
      ),
    );
  }
}

Trip? _upcomingTrip(List<Trip> trips) {
  final today = DateTime.now();
  final candidates = trips
      .where(
        (trip) =>
            !trip.endDate.isBefore(DateTime(today.year, today.month, today.day)),
      )
      .toList()
    ..sort((a, b) => a.startDate.compareTo(b.startDate));
  return candidates.isEmpty ? null : candidates.first;
}

class _JourneyImage extends StatelessWidget {
  const _JourneyImage({required this.tripName});

  final String tripName;

  @override
  Widget build(BuildContext context) {
    final source = destinationHeroAsset(tripName);
    if (source == null) {
      return const ColoredBox(
        color: CeylonColors.forest,
        child: Center(
          child: Icon(Icons.photo_outlined, color: CeylonColors.mint),
        ),
      );
    }
    return Image.asset(
      source,
      fit: BoxFit.cover,
      errorBuilder: (_, __, ___) => const ColoredBox(
        color: CeylonColors.forest,
        child: Center(
          child: Icon(Icons.photo_outlined, color: CeylonColors.mint),
        ),
      ),
    );
  }
}

class _MissingDestinationImage extends StatelessWidget {
  const _MissingDestinationImage({required this.attraction});

  final AttractionModel attraction;

  @override
  Widget build(BuildContext context) => ColoredBox(
    color: CeylonColors.forest,
    child: Center(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Text(
          attraction.district,
          textAlign: TextAlign.center,
          style: const TextStyle(color: Colors.white70, fontWeight: FontWeight.w700),
        ),
      ),
    ),
  );
}
