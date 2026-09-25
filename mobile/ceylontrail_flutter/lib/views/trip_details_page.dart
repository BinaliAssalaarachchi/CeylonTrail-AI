import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/trip_model.dart';
import '../services/api_client.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';
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
      if (mounted) {
        setState(() {
          _trip = trip;
          _loading = false;
        });
      }
    } catch (error) {
      if (mounted) {
        setState(() {
          _error = error.toString();
          _loading = false;
        });
      }
    }
  }

  Future<void> _delete() async {
    final confirmed =
        await showDialog<bool>(
          context: context,
          builder: (context) => AlertDialog(
            title: const Text('Delete this trip?'),
            content: const Text('This action cannot be undone.'),
            actions: [
              TextButton(
                onPressed: () => context.pop(false),
                child: const Text('Cancel'),
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
      ).showSnackBar(const SnackBar(content: Text('Trip deleted.')));
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
      ).showSnackBar(const SnackBar(content: Text('Itinerary generated.')));
      await context.push('/trips/${widget.id}/itinerary');
      if (mounted) _load();
    } on ApiException catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error.message)));
      }
    } finally {
      if (mounted) setState(() => _generating = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (_error != null) {
      return Scaffold(
        appBar: AppBar(),
        body: ErrorState(message: _error!, onRetry: _load),
      );
    }

    final trip = _trip!;
    final canGenerate = trip.status == 'Draft' || trip.status == 'Planned';
    return Scaffold(
      appBar: AppBar(
        title: const Text('Trip details'),
        actions: [
          IconButton(
            onPressed: _deleting ? null : _delete,
            icon: const Icon(Icons.delete_outline),
            tooltip: 'Delete trip',
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(CeylonSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(trip.name, style: Theme.of(context).textTheme.displaySmall),
            const SizedBox(height: CeylonSpacing.sm),
            TripStatusChip(status: trip.status),
            const SizedBox(height: CeylonSpacing.md),
            Card(
              child: Padding(
                padding: const EdgeInsets.all(CeylonSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${displayDate(trip.startDate)} – ${displayDate(trip.endDate)}',
                    ),
                    const SizedBox(height: 8),
                    Text(
                      displayMoney(trip.budget),
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: CeylonSpacing.lg),
            Text('Preferences', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: CeylonSpacing.sm),
            PreferenceList(preferences: trip.preferences),
            const SizedBox(height: CeylonSpacing.sm),
            OutlinedButton.icon(
              onPressed: () async {
                await context.push('/trips/${trip.id}/preferences');
                if (mounted) _load();
              },
              icon: const Icon(Icons.tune),
              label: const Text('Manage preferences'),
            ),
            const SizedBox(height: CeylonSpacing.lg),
            Text('Trip actions', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: CeylonSpacing.sm),
            ElevatedButton.icon(
              onPressed: () async {
                final result = await context.push(
                  '/trips/${trip.id}/edit',
                  extra: trip,
                );
                if (result != null && mounted) _load();
              },
              icon: const Icon(Icons.edit_outlined),
              label: const Text('Edit trip'),
            ),
            if (canGenerate) ...[
              const SizedBox(height: CeylonSpacing.sm),
              OutlinedButton.icon(
                onPressed: _generating ? null : _generate,
                icon: _generating
                    ? const SizedBox(
                        height: 18,
                        width: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.auto_awesome_outlined),
                label: Text(
                  _generating
                      ? 'Generating itinerary...'
                      : 'Generate itinerary',
                ),
              ),
            ],
            const SizedBox(height: CeylonSpacing.sm),
            OutlinedButton.icon(
              onPressed: () => context.push('/trips/${trip.id}/itinerary'),
              icon: const Icon(Icons.route_outlined),
              label: const Text('View generated itinerary'),
            ),
            const SizedBox(height: CeylonSpacing.sm),
            OutlinedButton.icon(
              onPressed: () => context.push('/trips/${trip.id}/travel-safety'),
              icon: const Icon(Icons.shield_outlined),
              label: const Text('Travel Safety'),
            ),
          ],
        ),
      ),
    );
  }
}
