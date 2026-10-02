import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/trip_model.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';
import '../utils/attraction_images.dart';
import 'trip_widgets.dart';

class MyTripsPage extends StatefulWidget {
  const MyTripsPage({required this.api, super.key});
  final TripApiService api;
  @override State<MyTripsPage> createState() => _MyTripsPageState();
}

enum _TripFilter { upcoming, past, all }

class _MyTripsPageState extends State<MyTripsPage> {
  var _loading = true;
  var _itinerariesLoading = false;
  String? _error;
  List<Trip> _trips = [];
  Map<String, Itinerary?> _itineraries = {};
  _TripFilter _filter = _TripFilter.upcoming;

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final trips = await widget.api.getTrips();
      if (!mounted) return;
      setState(() { _trips = trips; _itineraries = {}; _loading = false; _itinerariesLoading = trips.isNotEmpty; });
      await _loadItineraries(trips);
    } catch (error) {
      if (mounted) setState(() { _error = error.toString(); _loading = false; _itinerariesLoading = false; });
    }
  }

  Future<void> _loadItineraries(List<Trip> trips) async {
    final results = <String, Itinerary?>{};
    await Future.wait(trips.map((trip) async { try { results[trip.id] = await widget.api.getItinerary(trip.id); } catch (_) {} }));
    if (mounted) setState(() { _itineraries = results; _itinerariesLoading = false; });
  }

  DateTime get _today { final now = DateTime.now(); return DateTime(now.year, now.month, now.day); }
  bool _isUpcoming(Trip trip) => !trip.endDate.isBefore(_today);

  List<Trip> get _visibleTrips {
    var trips = switch (_filter) {
      _TripFilter.upcoming => _trips.where(_isUpcoming).toList(),
      _TripFilter.past => _trips.where((trip) => !_isUpcoming(trip)).toList(),
      _TripFilter.all => [..._trips],
    };
    trips.sort((a, b) => a.startDate.compareTo(b.startDate));
    if (_filter == _TripFilter.past) trips = trips.reversed.toList();
    return trips;
  }

  Trip? get _nextTrip { final upcoming = _trips.where(_isUpcoming).toList()..sort((a, b) => a.startDate.compareTo(b.startDate)); return upcoming.isEmpty ? null : upcoming.first; }
  Future<void> _openTrip(Trip trip) async { await context.push('/trips/' + trip.id); if (mounted) _load(); }

  @override
  Widget build(BuildContext context) => Scaffold(
    body: SafeArea(child: Center(child: ConstrainedBox(constraints: const BoxConstraints(maxWidth: 760), child: _content(context)))),
    floatingActionButton: FloatingActionButton.extended(onPressed: () async { await context.push('/trips/create'); if (mounted) _load(); }, icon: const Icon(Icons.add), label: const Text('Plan a trip')),
  );

  Widget _content(BuildContext context) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) return ErrorState(message: _error!, onRetry: _load);
    if (_trips.isEmpty) {
      return RefreshIndicator(onRefresh: _load, child: ListView(padding: const EdgeInsets.fromLTRB(CeylonSpacing.lg, 80, CeylonSpacing.lg, 120), children: [
        const Icon(Icons.landscape_outlined, size: 58, color: CeylonColors.tea), const SizedBox(height: CeylonSpacing.lg),
        Text('Your next Sri Lankan story starts here.', textAlign: TextAlign.center, style: Theme.of(context).textTheme.headlineMedium), const SizedBox(height: CeylonSpacing.sm),
        const Text('Create a trip and let CeylonTrail help shape the journey.', textAlign: TextAlign.center), const SizedBox(height: CeylonSpacing.lg),
        ElevatedButton.icon(onPressed: () => context.push('/trips/create'), icon: const Icon(Icons.add), label: const Text('Plan my first trip')),
      ]));
    }
    final next = _nextTrip;
    final visible = _visibleTrips;
    final remaining = visible.where((trip) => trip.id != next?.id).toList();
    return RefreshIndicator(onRefresh: _load, child: ListView(padding: const EdgeInsets.fromLTRB(CeylonSpacing.md, CeylonSpacing.lg, CeylonSpacing.md, 120), children: [
      Row(crossAxisAlignment: CrossAxisAlignment.start, children: [Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text('My journeys', style: Theme.of(context).textTheme.displaySmall), const SizedBox(height: 6), const Text('Your Sri Lankan adventures, all in one place.')])), IconButton(onPressed: _load, icon: const Icon(Icons.refresh_rounded), tooltip: 'Refresh trips')]),
      const SizedBox(height: CeylonSpacing.lg), Wrap(spacing: CeylonSpacing.sm, children: [_filterChip('Upcoming', _TripFilter.upcoming), _filterChip('Past', _TripFilter.past), _filterChip('All', _TripFilter.all)]),
      if (_filter == _TripFilter.upcoming && next != null) ...[const SizedBox(height: CeylonSpacing.xl), _label('YOUR NEXT JOURNEY'), const SizedBox(height: CeylonSpacing.sm), _FeaturedTripCard(trip: next, itinerary: _itineraries[next.id], itineraryLoading: _itinerariesLoading, onTap: () => _openTrip(next))],
      if (remaining.isNotEmpty) ...[const SizedBox(height: CeylonSpacing.xl), _label(_filter == _TripFilter.past ? 'PAST JOURNEYS' : 'YOUR JOURNEYS'), const SizedBox(height: CeylonSpacing.sm), ...remaining.map((trip) => Padding(padding: const EdgeInsets.only(bottom: CeylonSpacing.sm), child: _TripCard(trip: trip, itinerary: _itineraries[trip.id], itineraryLoading: _itinerariesLoading, onTap: () => _openTrip(trip))))],
      if (visible.isEmpty) const Padding(padding: EdgeInsets.symmetric(vertical: CeylonSpacing.xl), child: Center(child: Text('No journeys in this view yet.'))),
    ]));
  }

  Widget _filterChip(String label, _TripFilter filter) => ChoiceChip(label: Text(label), selected: _filter == filter, onSelected: (_) => setState(() => _filter = filter), selectedColor: CeylonColors.mint, side: BorderSide.none, labelStyle: TextStyle(color: _filter == filter ? CeylonColors.forest : CeylonColors.inkMuted, fontWeight: FontWeight.w700));
  Widget _label(String text) => Text(text, style: const TextStyle(color: CeylonColors.inkMuted, fontSize: 12, fontWeight: FontWeight.w800, letterSpacing: 1.2));
}

class _FeaturedTripCard extends StatelessWidget {
  const _FeaturedTripCard({required this.trip, required this.itinerary, required this.itineraryLoading, required this.onTap});
  final Trip trip; final Itinerary? itinerary; final bool itineraryLoading; final VoidCallback onTap;
  @override
  Widget build(BuildContext context) {
    final asset = destinationHeroAsset(trip.name);
    return GestureDetector(onTap: onTap, child: ClipRRect(borderRadius: BorderRadius.circular(CeylonRadii.card), child: AspectRatio(aspectRatio: 1.38, child: Stack(fit: StackFit.expand, children: [
      if (asset != null) Image.asset(asset, fit: BoxFit.cover) else Container(color: CeylonColors.forest),
      const DecoratedBox(decoration: BoxDecoration(gradient: LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Colors.transparent, Color(0xD900241A)]))),
      Padding(padding: const EdgeInsets.all(CeylonSpacing.lg), child: Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisAlignment: MainAxisAlignment.end, children: [
        TripStatusChip(status: itineraryLoading ? 'Pending' : trip.status), const SizedBox(height: CeylonSpacing.sm), Text(trip.name, maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(color: Colors.white, fontSize: 26, fontWeight: FontWeight.w700)), const SizedBox(height: 5),
        Text(displayDate(trip.startDate) + ' \u2013 ' + displayDate(trip.endDate), style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w700)), const SizedBox(height: 4), Text(tripDuration(trip) + '  \u2022  ' + displayMoney(trip.budget), style: const TextStyle(color: Colors.white)), const SizedBox(height: CeylonSpacing.md),
        const Row(children: [Text('View journey', style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800)), SizedBox(width: 6), Icon(Icons.arrow_forward_rounded, color: Colors.white, size: 18)]),
      ])),
    ]))));
  }
}

class _TripCard extends StatelessWidget {
  const _TripCard({required this.trip, required this.itinerary, required this.itineraryLoading, required this.onTap});
  final Trip trip; final Itinerary? itinerary; final bool itineraryLoading; final VoidCallback onTap;
  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(builder: (context, constraints) {
      final narrow = constraints.maxWidth <= 420;
      final content = Padding(padding: const EdgeInsets.all(CeylonSpacing.md), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(trip.name, maxLines: 2, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.titleMedium), const SizedBox(height: 5),
        Text(displayDate(trip.startDate) + ' \u2013 ' + displayDate(trip.endDate), style: const TextStyle(fontWeight: FontWeight.w600, color: CeylonColors.inkMuted)), const SizedBox(height: 4),
        Text(tripDuration(trip) + '  \u2022  ' + displayMoney(trip.budget), style: Theme.of(context).textTheme.bodySmall), const SizedBox(height: 10), TripStatusChip(status: itineraryLoading ? 'Pending' : trip.status),
      ]));
      final body = narrow ? Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [SizedBox(height: 170, child: _TripImage(asset: destinationHeroAsset(trip.name), iconSize: 48)), content]) : SizedBox(height: 150, child: Row(children: [SizedBox(width: 118, child: _TripImage(asset: destinationHeroAsset(trip.name), iconSize: 36)), Expanded(child: content), const Padding(padding: EdgeInsets.only(right: 12), child: Icon(Icons.chevron_right_rounded, color: CeylonColors.inkMuted))]));
      return Card(clipBehavior: Clip.antiAlias, child: InkWell(onTap: onTap, child: body));
    });
  }
}

class _TripImage extends StatelessWidget {
  const _TripImage({required this.asset, required this.iconSize});
  final String? asset; final double iconSize;
  @override Widget build(BuildContext context) => asset == null ? Container(color: CeylonColors.tea, child: Icon(Icons.landscape_outlined, color: Colors.white.withValues(alpha: .82), size: iconSize)) : Image.asset(asset!, fit: BoxFit.cover, errorBuilder: (_, _, _) => Container(color: CeylonColors.tea, child: Icon(Icons.landscape_outlined, color: Colors.white, size: iconSize)));
}
