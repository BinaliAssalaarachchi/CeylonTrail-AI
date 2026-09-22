import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../theme/app_theme.dart';
import '../widgets/auth_scope.dart';
import '../widgets/mobile_shell.dart';

class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final authService = AuthScope.of(context);
    final user = authService.user!;
    final firstName = user.firstName.isEmpty ? 'traveller' : user.firstName;

    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(
          CeylonSpacing.lg,
          CeylonSpacing.md,
          CeylonSpacing.lg,
          CeylonSpacing.xl,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            MobileHeader(userName: user.displayName),
            const SizedBox(height: CeylonSpacing.xl),
            Text(
              'Ayubowan, $firstName.',
              style: Theme.of(context).textTheme.displaySmall,
            ),
            const SizedBox(height: CeylonSpacing.sm),
            Text(
              'Find a slower route through the island.',
              style: Theme.of(context).textTheme.bodyLarge,
            ),
            const SizedBox(height: CeylonSpacing.lg),
            _DiscoveryEntry(),
            const SizedBox(height: CeylonSpacing.lg),
            _HeroDestination(),
            const SizedBox(height: CeylonSpacing.xl),
            _SectionHeading(
              title: 'Plan with intention',
              actionLabel: 'Coming soon',
            ),
            const SizedBox(height: CeylonSpacing.md),
            _PlannerCard(),
            const SizedBox(height: CeylonSpacing.md),
            const _BookingsEntry(),
            const SizedBox(height: CeylonSpacing.xl),
            _SectionHeading(
              title: 'A little closer to Ceylon',
              actionLabel: 'Explore',
            ),
            const SizedBox(height: CeylonSpacing.md),
            const _DestinationRow(),
          ],
        ),
      ),
    );
  }
}

class _DiscoveryEntry extends StatelessWidget {
  @override
  Widget build(BuildContext context) => Semantics(
    label: 'Search destinations and experiences. Discovery is coming soon.',
    button: true,
    child: Container(
      padding: const EdgeInsets.symmetric(
        horizontal: CeylonSpacing.md,
        vertical: 14,
      ),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(CeylonRadii.field),
        boxShadow: const [
          BoxShadow(
            color: Color(0x100E3B2E),
            blurRadius: 18,
            offset: Offset(0, 8),
          ),
        ],
      ),
      child: Row(
        children: [
          const Icon(Icons.search, color: CeylonColors.tea),
          const SizedBox(width: CeylonSpacing.sm),
          Expanded(
            child: Text(
              'Where will the trail take you?',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ),
          const Icon(Icons.tune, color: CeylonColors.inkMuted, size: 20),
        ],
      ),
    ),
  );
}

class _HeroDestination extends StatelessWidget {
  @override
  Widget build(BuildContext context) => ClipRRect(
    borderRadius: BorderRadius.circular(CeylonRadii.card),
    child: SizedBox(
      height: 300,
      width: double.infinity,
      child: Stack(
        fit: StackFit.expand,
        children: [
          Image.asset(
            'assets/images/tea-country-hero.jpg',
            fit: BoxFit.cover,
            semanticLabel: 'Sunlit tea country hills in Sri Lanka',
          ),
          DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: [
                  Colors.transparent,
                  CeylonColors.forestDeep.withValues(alpha: 0.9),
                ],
              ),
            ),
          ),
          Positioned(
            left: 20,
            right: 20,
            bottom: 20,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'THE HIGHLANDS',
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                    color: CeylonColors.mint,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 1.5,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  'Mist, tea and mountain roads.',
                  style: Theme.of(
                    context,
                  ).textTheme.headlineMedium?.copyWith(color: Colors.white),
                ),
                const SizedBox(height: 5),
                Text(
                  'A visual invitation to travel with more presence.',
                  style: Theme.of(
                    context,
                  ).textTheme.bodyMedium?.copyWith(color: Colors.white70),
                ),
              ],
            ),
          ),
        ],
      ),
    ),
  );
}

class _PlannerCard extends StatelessWidget {
  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(CeylonSpacing.lg),
    decoration: BoxDecoration(
      color: CeylonColors.forest,
      borderRadius: BorderRadius.circular(CeylonRadii.card),
    ),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(Icons.auto_awesome, color: CeylonColors.mint, size: 26),
        const SizedBox(width: CeylonSpacing.md),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'CeylonTrail Trip Planner',
                style: Theme.of(
                  context,
                ).textTheme.titleLarge?.copyWith(color: Colors.white),
              ),
              const SizedBox(height: 6),
              Text(
                'Shape a thoughtful journey around the places and pace you love.',
                style: Theme.of(
                  context,
                ).textTheme.bodyMedium?.copyWith(color: Colors.white70),
              ),
              const SizedBox(height: CeylonSpacing.md),
              Text(
                'COMING IN A FUTURE FEATURE PHASE',
                style: Theme.of(context).textTheme.labelSmall?.copyWith(
                  color: CeylonColors.mint,
                  fontWeight: FontWeight.w800,
                  letterSpacing: 1.1,
                ),
              ),
            ],
          ),
        ),
      ],
    ),
  );
}

class _BookingsEntry extends StatelessWidget {
  const _BookingsEntry();

  @override
  Widget build(BuildContext context) => Semantics(
    button: true,
    label: 'Open My Bookings',
    child: Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(CeylonRadii.card),
        onTap: () => context.go('/bookings'),
        child: Padding(
          padding: const EdgeInsets.all(CeylonSpacing.md),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(CeylonSpacing.sm),
                decoration: BoxDecoration(
                  color: CeylonColors.mint,
                  borderRadius: BorderRadius.circular(CeylonRadii.field),
                ),
                child: const Icon(
                  Icons.bookmark_border,
                  color: CeylonColors.forest,
                ),
              ),
              const SizedBox(width: CeylonSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'My Bookings',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 3),
                    Text(
                      'View your reservations and booking history.',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right, color: CeylonColors.inkMuted),
            ],
          ),
        ),
      ),
    ),
  );
}

class _SectionHeading extends StatelessWidget {
  const _SectionHeading({required this.title, required this.actionLabel});
  final String title;
  final String actionLabel;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisAlignment: MainAxisAlignment.spaceBetween,
    children: [
      Text(title, style: Theme.of(context).textTheme.titleLarge),
      Text(
        actionLabel,
        style: Theme.of(context).textTheme.labelMedium?.copyWith(
          color: CeylonColors.tea,
          fontWeight: FontWeight.w800,
        ),
      ),
    ],
  );
}

class _DestinationRow extends StatelessWidget {
  const _DestinationRow();

  @override
  Widget build(BuildContext context) => SizedBox(
    height: 160,
    child: ListView(
      scrollDirection: Axis.horizontal,
      children: const [
        _DestinationCard(
          image: 'assets/images/sigiriya-hero.png',
          title: 'Sigiriya',
          subtitle: 'Cultural heart',
        ),
        _DestinationCard(
          image: 'assets/images/tea-country-hero.jpg',
          title: 'Tea country',
          subtitle: 'Highland quiet',
        ),
      ],
    ),
  );
}

class _DestinationCard extends StatelessWidget {
  const _DestinationCard({
    required this.image,
    required this.title,
    required this.subtitle,
  });
  final String image;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) => Container(
    width: 220,
    margin: const EdgeInsets.only(right: CeylonSpacing.md),
    clipBehavior: Clip.antiAlias,
    decoration: BoxDecoration(
      borderRadius: BorderRadius.circular(CeylonRadii.card),
    ),
    child: Stack(
      fit: StackFit.expand,
      children: [
        Image.asset(
          image,
          fit: BoxFit.cover,
          semanticLabel: '$title in Sri Lanka',
        ),
        DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: [
                Colors.transparent,
                CeylonColors.forestDeep.withValues(alpha: 0.9),
              ],
            ),
          ),
        ),
        Positioned(
          left: 14,
          bottom: 14,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: Theme.of(
                  context,
                ).textTheme.titleMedium?.copyWith(color: Colors.white),
              ),
              Text(
                subtitle,
                style: Theme.of(
                  context,
                ).textTheme.bodySmall?.copyWith(color: Colors.white70),
              ),
            ],
          ),
        ),
      ],
    ),
  );
}
