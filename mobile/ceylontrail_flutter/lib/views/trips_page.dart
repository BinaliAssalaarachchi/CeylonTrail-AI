import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/trip_model.dart';
import '../services/api_client.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/auth_scope.dart';

class TripsPage extends StatefulWidget {
  const TripsPage({super.key});

  @override
  State<TripsPage> createState() => _TripsPageState();
}

class _TripsPageState extends State<TripsPage> {
  TripApiService? _service;
  List<Trip> _trips = const [];
  bool _loading = true;
  bool _hasLoaded = false;
  String? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _service ??= TripApiService(ApiClient(storage: AuthScope.of(context).storage));
    if (!_hasLoaded) {
      _hasLoaded = true;
      _load();
    }
  }

  Future<void> _load() async {
    if (mounted) setState(() { _loading = true; _error = null; });
    try {
      final trips = await _service!.getTrips();
      if (mounted) setState(() => _trips = trips);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } catch (_) {
      if (mounted) setState(() => _error = 'Trips are unavailable. Please try again.');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _confirmDelete(Trip trip) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete trip?'),
        content: Text('Are you sure you want to delete "${trip.name.isEmpty ? 'this trip' : trip.name}"? This action cannot be undone.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: Colors.red),
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Delete'),
          ),
        ],
      ),
    ) ?? false;

    if (!confirmed || !mounted) return;

    try {
      await _service!.deleteTrip(trip.id);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Trip deleted.')));
      await _load();
    } on ApiException catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Failed to delete trip.')));
      }
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('My Trips')),
    body: RefreshIndicator(
      onRefresh: _load,
      child: _buildBody(context),
    ),
  );

  Widget _buildBody(BuildContext context) {
    if (_loading) {
      return ListView(children: const [SizedBox(height: 220), Center(child: CircularProgressIndicator())]);
    }
    if (_error != null) {
      return ListView(children: [
        const SizedBox(height: 150),
        _StateMessage(
          icon: Icons.cloud_off_outlined,
          title: 'Trips are unavailable',
          message: _error!,
          action: _load,
        ),
      ]);
    }
    if (_trips.isEmpty) {
      return ListView(children: const [
        SizedBox(height: 150),
        _StateMessage(
          icon: Icons.route_outlined,
          title: 'No trips yet',
          message: 'Your planned trips will appear here.',
        ),
      ]);
    }

    return ListView.builder(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(CeylonSpacing.md, CeylonSpacing.sm, CeylonSpacing.md, CeylonSpacing.xl),
      itemCount: _trips.length,
      itemBuilder: (context, index) => Padding(
        padding: const EdgeInsets.only(bottom: CeylonSpacing.md),
        child: _TripCard(
          trip: _trips[index],
          onDelete: () => _confirmDelete(_trips[index]),
        ),
      ),
    );
  }
}

class _TripCard extends StatelessWidget {
  const _TripCard({required this.trip, required this.onDelete});

  final Trip trip;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) => Card(
    child: InkWell(
      borderRadius: BorderRadius.circular(CeylonRadii.card),
      onTap: () => context.push('/trips/${trip.id}'),
      child: Padding(
        padding: const EdgeInsets.all(CeylonSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(child: Text(trip.name.isEmpty ? 'Unnamed trip' : trip.name, style: Theme.of(context).textTheme.titleLarge)),
                if (trip.status.isNotEmpty) Chip(label: Text(_label(trip.status))),
                const SizedBox(width: 4),
                IconButton(
                  icon: const Icon(Icons.delete_outline, size: 20, color: CeylonColors.inkMuted),
                  tooltip: 'Delete trip',
                  onPressed: onDelete,
                ),
              ],
            ),
            const SizedBox(height: CeylonSpacing.sm),
            Text(_dateRange(trip), style: Theme.of(context).textTheme.bodyMedium),
            const SizedBox(height: CeylonSpacing.sm),
            Text('Budget: LKR ${trip.budget.toStringAsFixed(2)}', style: Theme.of(context).textTheme.bodyMedium),
            const SizedBox(height: CeylonSpacing.md),
            Row(
              children: [
                Text('Created ${_date(trip.createdAt)}', style: Theme.of(context).textTheme.bodySmall),
                const Spacer(),
                const Icon(Icons.chevron_right, color: CeylonColors.inkMuted),
              ],
            ),
          ],
        ),
      ),
    ),
  );
}

class _StateMessage extends StatelessWidget {
  const _StateMessage({required this.icon, required this.title, required this.message, this.action});

  final IconData icon;
  final String title;
  final String message;
  final VoidCallback? action;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.all(CeylonSpacing.xl),
    child: Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, color: CeylonColors.tea, size: 42),
        const SizedBox(height: CeylonSpacing.md),
        Text(title, style: Theme.of(context).textTheme.titleLarge, textAlign: TextAlign.center),
        const SizedBox(height: CeylonSpacing.sm),
        Text(message, textAlign: TextAlign.center),
        if (action != null) ...[
          const SizedBox(height: CeylonSpacing.md),
          OutlinedButton(onPressed: action, child: const Text('Try again')),
        ],
      ],
    ),
  );
}

String _dateRange(Trip trip) => '${_date(trip.startDate)} – ${_date(trip.endDate)}';
String _date(DateTime? value) => value == null ? 'Date unavailable' : '${value.day}/${value.month}/${value.year}';
String _label(String value) => value.replaceAllMapped(RegExp(r'([a-z])([A-Z])'), (match) => '${match.group(1)} ${match.group(2)}');
