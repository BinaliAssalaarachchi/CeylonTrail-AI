import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/trip_model.dart';
import '../services/api_client.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';
import '../utils/attraction_images.dart';
import 'trip_widgets.dart';

class TripDetailsPage extends StatefulWidget {
  const TripDetailsPage({required this.api, required this.id, super.key});
  final TripApiService api;
  final String id;
  @override
  State<TripDetailsPage> createState() => _TripDetailsPageState();
}

class _TripDetailsPageState extends State<TripDetailsPage> {
  Trip? _trip;
  Itinerary? _itinerary;
  String? _error;
  var _loading = true;
  var _deleting = false;
  var _generating = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final trip = await widget.api.getTrip(widget.id);
      Itinerary? itinerary;
      try {
        itinerary = await widget.api.getItinerary(widget.id);
      } on ApiException {
        // A missing itinerary is a normal state for a newly created trip.
      }
      if (mounted)
        setState(() {
          _trip = trip;
          _itinerary = itinerary;
          _loading = false;
        });
    } catch (error) {
      if (mounted)
        setState(() {
          _error = error.toString();
          _loading = false;
        });
    }
  }

  Future<void> _delete() async {
    final confirmed =
        await showDialog<bool>(
          context: context,
          builder: (context) => AlertDialog(
            title: const Text('Delete this journey?'),
            content: const Text('This action cannot be undone.'),
            actions: [
              TextButton(
                onPressed: () => context.pop(false),
                child: const Text('Keep journey'),
              ),
              FilledButton(
                onPressed: () => context.pop(true),
                child: const Text('Delete'),
              ),
            ],
          ),
        ) ??
        false;
    if (!confirmed) return;
    setState(() => _deleting = true);
    try {
      await widget.api.deleteTrip(widget.id);
      if (!mounted) return;
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Journey deleted.')));
      context.go('/trips');
    } on ApiException catch (error) {
      if (mounted) {
        setState(() => _deleting = false);
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error.message)));
      }
    }
  }

  Future<void> _generate() async {
    setState(() => _generating = true);
    try {
      await widget.api.generateItinerary(widget.id);
      if (!mounted) return;
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Your itinerary is ready.')));
      await context.push('/trips/${widget.id}/itinerary');
      if (mounted) _load();
    } on ApiException catch (error) {
      if (mounted)
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _generating = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading)
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    if (_error != null)
      return Scaffold(
        appBar: AppBar(),
        body: ErrorState(message: _error!, onRetry: _load),
      );
    final trip = _trip!;
    final canGenerate =
        _itinerary == null &&
        (trip.status == 'Draft' || trip.status == 'Planned');
    return Scaffold(
      appBar: AppBar(
        actions: [
          IconButton(
            onPressed: _deleting ? null : _delete,
            icon: const Icon(Icons.delete_outline),
            tooltip: 'Delete journey',
          ),
        ],
      ),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 760),
          child: RefreshIndicator(
            onRefresh: _load,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(
                CeylonSpacing.md,
                CeylonSpacing.sm,
                CeylonSpacing.md,
                48,
              ),
              children: [
                _TripHero(trip: trip),
                const SizedBox(height: CeylonSpacing.md),
                _SummaryCard(trip: trip),
                const SizedBox(height: CeylonSpacing.xl),
                Text(
                  'Your travel preferences',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: CeylonSpacing.xs),
                const Text(
                  'What you asked CeylonTrail to plan',
                  style: TextStyle(color: CeylonColors.inkMuted),
                ),
                const SizedBox(height: CeylonSpacing.sm),
                SectionCard(
                  child: PreferenceList(preferences: trip.preferences),
                ),
                const SizedBox(height: CeylonSpacing.sm),
                OutlinedButton.icon(
                  onPressed: () async {
                    await context.push('/trips/${trip.id}/preferences');
                    if (mounted) _load();
                  },
                  icon: const Icon(Icons.tune),
                  label: const Text('Manage preferences'),
                ),
                const SizedBox(height: CeylonSpacing.xl),
                Text(
                  'Your itinerary',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: CeylonSpacing.sm),
                SectionCard(
                  child: Row(
                    children: [
                      Container(
                        width: 46,
                        height: 46,
                        decoration: BoxDecoration(
                          color: CeylonColors.mint,
                          borderRadius: BorderRadius.circular(14),
                        ),
                        child: const Icon(
                          Icons.route_outlined,
                          color: CeylonColors.forest,
                        ),
                      ),
                      const SizedBox(width: CeylonSpacing.md),
                      const Expanded(
                        child: Text(
                          'Shape your days around the places and experiences you want to remember.',
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: CeylonSpacing.sm),
                if (canGenerate)
                  ElevatedButton.icon(
                    onPressed: _generating ? null : _generate,
                    icon: _generating
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Icon(Icons.auto_awesome_outlined),
                    label: Text(
                      _generating
                          ? 'Planning your itinerary...'
                          : 'Plan my itinerary',
                    ),
                  ),
                if (!canGenerate)
                  OutlinedButton.icon(
                    onPressed: () =>
                        context.push('/trips/${trip.id}/itinerary'),
                    icon: const Icon(Icons.visibility_outlined),
                    label: const Text('View itinerary'),
                  ),
                const SizedBox(height: CeylonSpacing.xl),
                Text(
                  'Journey actions',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: CeylonSpacing.sm),
                OutlinedButton.icon(
                  onPressed: () async {
                    final result = await context.push(
                      '/trips/${trip.id}/edit',
                      extra: trip,
                    );
                    if (result != null && mounted) _load();
                  },
                  icon: const Icon(Icons.edit_outlined),
                  label: const Text('Edit journey'),
                ),
                const SizedBox(height: CeylonSpacing.sm),
                OutlinedButton.icon(
                  onPressed: () =>
                      context.push('/trips/${trip.id}/travel-safety'),
                  icon: const Icon(Icons.shield_outlined),
                  label: const Text('Travel safety'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _TripHero extends StatelessWidget {
  const _TripHero({required this.trip});
  final Trip trip;
  @override
  Widget build(BuildContext context) {
    final asset = destinationHeroAsset(trip.name);
    return ClipRRect(
      borderRadius: BorderRadius.circular(CeylonRadii.card),
      child: SizedBox(
        height: 250,
        child: Stack(
          fit: StackFit.expand,
          children: [
            if (asset != null)
              Image.asset(asset, fit: BoxFit.cover)
            else
              Container(color: CeylonColors.forest),
            const DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  colors: [Colors.transparent, Color(0xE600241A)],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(CeylonSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  TripStatusChip(status: trip.status),
                  const SizedBox(height: CeylonSpacing.sm),
                  Text(
                    trip.name,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 28,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 5),
                  Text(
                    '${displayDate(trip.startDate)} – ${displayDate(trip.endDate)}',
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.trip});
  final Trip trip;
  @override
  Widget build(BuildContext context) => SectionCard(
    child: Wrap(
      spacing: 28,
      runSpacing: 14,
      children: [
        _SummaryItem(
          icon: Icons.calendar_today_outlined,
          label: 'Dates',
          value:
              '${displayDate(trip.startDate)} – ${displayDate(trip.endDate)}',
        ),
        _SummaryItem(
          icon: Icons.schedule_outlined,
          label: 'Duration',
          value: tripDuration(trip),
        ),
        _SummaryItem(
          icon: Icons.account_balance_wallet_outlined,
          label: 'Budget',
          value: displayMoney(trip.budget),
        ),
      ],
    ),
  );
}

class _SummaryItem extends StatelessWidget {
  const _SummaryItem({
    required this.icon,
    required this.label,
    required this.value,
  });
  final IconData icon;
  final String label;
  final String value;
  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Icon(icon, size: 19, color: CeylonColors.tea),
      const SizedBox(width: 8),
      Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: const TextStyle(fontSize: 12, color: CeylonColors.inkMuted),
          ),
          Text(value, style: const TextStyle(fontWeight: FontWeight.w800)),
        ],
      ),
    ],
  );
}
