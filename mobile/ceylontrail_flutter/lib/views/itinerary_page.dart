import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/trip_model.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';
import 'trip_widgets.dart';

class ItineraryPage extends StatefulWidget {
  const ItineraryPage({required this.api, required this.tripId, super.key});

  final TripApiService api;
  final String tripId;

  @override
  State<ItineraryPage> createState() => _ItineraryPageState();
}

class _ItineraryPageState extends State<ItineraryPage> {
  Itinerary? _itinerary;
  List<ItineraryHistoryItem> _history = const [];
  String? _error;
  var _loading = true;

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
      final results = await Future.wait([
        widget.api.getItinerary(widget.tripId),
        widget.api.getItineraryHistory(widget.tripId),
      ]);
      final itinerary = results[0] as Itinerary?;
      final history = results[1] as List<ItineraryHistoryItem>;
      if (mounted) {
        setState(() {
          _itinerary = itinerary;
          _history = history;
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
    if (_loading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (_error != null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Generated itinerary')),
        body: ErrorState(message: _error!, onRetry: _load),
      );
    }

    final itinerary = _itinerary;
    return Scaffold(
      appBar: AppBar(title: const Text('Generated itinerary')),
      body: itinerary == null
          ? const Center(
              child: Padding(
                padding: EdgeInsets.all(CeylonSpacing.lg),
                child: Text(
                  'No generated itinerary is available for this trip yet.',
                  textAlign: TextAlign.center,
                ),
              ),
            )
          : ListView(
              padding: const EdgeInsets.all(CeylonSpacing.md),
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'Itinerary',
                      style: Theme.of(context).textTheme.headlineMedium,
                    ),
                    TripStatusChip(status: itinerary.status),
                  ],
                ),
                const SizedBox(height: CeylonSpacing.sm),
                Text('Estimated cost: ${displayMoney(itinerary.totalEstimatedCost)}'),
                if (itinerary.createdAt != null) Text('Generated ${displayDate(itinerary.createdAt!)}'),
                const SizedBox(height: CeylonSpacing.lg),
                ...itinerary.days.map(
                  (day) => Card(
                    child: ListTile(
                      title: Text('Day ${day.dayNumber}'),
                      subtitle: Text(
                        '${displayDate(day.date)}\n${day.items.length} activities',
                      ),
                      isThreeLine: true,
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () => context.push(
                        '/trips/${widget.tripId}/itinerary/day/${day.dayNumber}',
                        extra: day,
                      ),
                    ),
                  ),
                ),
                const SizedBox(height: CeylonSpacing.lg),
                Text('Previous versions', style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: CeylonSpacing.sm),
                if (_history.length <= 1)
                  const Text('No previous itinerary versions are available.')
                else
                  ..._history.skip(1).map((version) => Card(
                    child: ListTile(
                      title: Text(version.createdAt == null ? 'Itinerary version' : displayDate(version.createdAt!)),
                      subtitle: Text('${version.status} • ${displayMoney(version.totalEstimatedCost)}'),
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () async {
                        final selected = await widget.api.getItineraryVersion(widget.tripId, version.id);
                        if (mounted) setState(() => _itinerary = selected);
                      },
                    ),
                  )),
              ],
            ),
    );
  }
}
