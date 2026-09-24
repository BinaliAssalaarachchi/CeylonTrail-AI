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
      final itinerary = await widget.api.getItinerary(widget.tripId);
      if (mounted) {
        setState(() {
          _itinerary = itinerary;
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
                Text(
                  'Estimated cost: ${displayMoney(itinerary.totalEstimatedCost)}',
                ),
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
              ],
            ),
    );
  }
}
