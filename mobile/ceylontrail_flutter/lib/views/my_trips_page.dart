import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/trip_model.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';
import 'trip_widgets.dart';

class MyTripsPage extends StatefulWidget {
  const MyTripsPage({required this.api, super.key});

  final TripApiService api;

  @override
  State<MyTripsPage> createState() => _MyTripsPageState();
}

class _MyTripsPageState extends State<MyTripsPage> {
  var _loading = true;
  String? _error;
  List<Trip> _trips = [];

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
      final trips = await widget.api.getTrips();
      if (mounted) {
        setState(() {
          _trips = trips;
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

  @override
  Widget build(BuildContext context) {
    Widget content;
    if (_loading) {
      content = const Center(child: CircularProgressIndicator());
    } else if (_error != null) {
      content = ErrorState(message: _error!, onRetry: _load);
    } else if (_trips.isEmpty) {
      content = RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          children: const [
            SizedBox(height: 160),
            Icon(Icons.route_outlined, size: 56),
            SizedBox(height: 16),
            Center(child: Text("You haven't created any trips yet.")),
          ],
        ),
      );
    } else {
      content = RefreshIndicator(
        onRefresh: _load,
        child: ListView.separated(
          padding: const EdgeInsets.all(CeylonSpacing.md),
          itemCount: _trips.length,
          separatorBuilder: (_, _) => const SizedBox(height: CeylonSpacing.sm),
          itemBuilder: (context, index) {
            final trip = _trips[index];
            return Card(
              child: ListTile(
                contentPadding: const EdgeInsets.all(CeylonSpacing.md),
                title: Text(
                  trip.name,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                subtitle: Text(
                  '${displayDate(trip.startDate)} – '
                  '${displayDate(trip.endDate)}\n'
                  '${displayMoney(trip.budget)}',
                ),
                isThreeLine: true,
                trailing: TripStatusChip(status: trip.status),
                onTap: () => context.push('/trips/${trip.id}'),
              ),
            );
          },
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: const Text('My trips'),
        actions: [
          IconButton(
            onPressed: _load,
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh trips',
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () async {
          await context.push('/trips/create');
          if (mounted) _load();
        },
        icon: const Icon(Icons.add),
        label: const Text('Create trip'),
      ),
      body: content,
    );
  }
}
