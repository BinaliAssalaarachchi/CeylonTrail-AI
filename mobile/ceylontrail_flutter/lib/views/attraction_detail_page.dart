import 'package:flutter/material.dart';

import '../models/attraction_model.dart';
import '../services/attraction_api_service.dart';
import '../services/api_client.dart';
import '../theme/app_theme.dart';
import '../widgets/auth_scope.dart';

class AttractionDetailPage extends StatefulWidget {
  const AttractionDetailPage({required this.id, super.key});
  final String id;

  @override
  State<AttractionDetailPage> createState() => _AttractionDetailPageState();
}

class _AttractionDetailPageState extends State<AttractionDetailPage> {
  late AttractionApiService _service;
  AttractionModel? _attraction;
  AvailabilityModel? _availability;
  bool _loading = true;
  bool _favoriteBusy = false;
  String? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _service = AttractionApiService(ApiClient(storage: AuthScope.of(context).storage));
    if (_attraction == null && _error == null) _load();
  }

  Future<void> _load() async {
    try {
      final attraction = await _service.getAttraction(widget.id);
      final availability = await _service.getAvailability(widget.id);
      if (mounted) setState(() { _attraction = attraction; _availability = availability; });
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _toggleFavorite() async {
    final attraction = _attraction;
    if (attraction == null) return;
    setState(() => _favoriteBusy = true);
    try {
      if (attraction.isFavorite) await _service.removeFavorite(attraction.id); else await _service.addFavorite(attraction.id);
      final refreshed = await _service.getAttraction(widget.id);
      if (mounted) setState(() => _attraction = refreshed);
    } catch (error) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.toString())));
    } finally {
      if (mounted) setState(() => _favoriteBusy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Scaffold(body: Center(child: CircularProgressIndicator()));
    if (_error != null || _attraction == null) return Scaffold(appBar: AppBar(), body: _errorState());
    final attraction = _attraction!;
    final isTourist = AuthScope.of(context).user?.role == 'Tourist';
    return Scaffold(
      appBar: AppBar(
        title: Text(attraction.name),
        actions: [if (isTourist) IconButton(tooltip: attraction.isFavorite ? 'Remove favorite' : 'Add favorite', onPressed: _favoriteBusy ? null : _toggleFavorite, icon: Icon(attraction.isFavorite ? Icons.favorite : Icons.favorite_border))],
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 28),
          children: [
            if (attraction.images.isNotEmpty) ClipRRect(borderRadius: BorderRadius.circular(CeylonRadii.card), child: Image.network(attraction.images.first.imageUrl, height: 230, fit: BoxFit.cover, errorBuilder: (_, __, ___) => _fallbackImage())) else _fallbackImage(),
            const SizedBox(height: 20),
            Text(attraction.name, style: Theme.of(context).textTheme.headlineMedium),
            const SizedBox(height: 8),
            Text('${attraction.category?.name ?? 'Experience'} · ${attraction.district}', style: Theme.of(context).textTheme.bodyLarge),
            const SizedBox(height: 8),
            Text(attraction.price == 0 ? 'Free entry' : 'From ${attraction.price.toStringAsFixed(2)}', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 18),
            Text(attraction.description, style: Theme.of(context).textTheme.bodyLarge),
            const SizedBox(height: 18),
            _InfoSection(title: 'Address', child: Text(attraction.address)),
            _InfoSection(title: 'Opening schedule', child: attraction.schedules.isEmpty ? const Text('No schedule information available.') : Column(children: attraction.schedules.map((schedule) => ListTile(contentPadding: EdgeInsets.zero, title: Text(schedule.dayOfWeek), trailing: Text(schedule.isClosed ? 'Closed' : '${_time(schedule.openingTime)} – ${_time(schedule.closingTime)}'))).toList())),
            _InfoSection(title: 'Availability', child: _availability?.slots.isEmpty ?? true ? const Text('No availability data for the selected period.') : Column(children: _availability!.slots.map((slot) => ListTile(contentPadding: EdgeInsets.zero, title: Text(slot.date.toLocal().toString().split(' ').first), subtitle: Text('${_time(slot.startTime)} – ${_time(slot.endTime)}'), trailing: Text('${slot.availableCapacity}/${slot.capacity} available'))).toList())),
          ],
        ),
      ),
    );
  }

  Widget _errorState() => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [const Text('Unable to load this attraction.'), const SizedBox(height: 12), FilledButton(onPressed: () { setState(() { _loading = true; _error = null; }); _load(); }, child: const Text('Retry'))]));
  Widget _fallbackImage() => Container(height: 230, decoration: BoxDecoration(color: CeylonColors.mint, borderRadius: BorderRadius.circular(CeylonRadii.card)), child: const Center(child: Icon(Icons.landscape_outlined, size: 64, color: CeylonColors.tea)));
  static String _time(String? value) => value == null ? '' : value.substring(0, value.length >= 5 ? 5 : value.length);
}

class _InfoSection extends StatelessWidget {
  const _InfoSection({required this.title, required this.child});
  final String title;
  final Widget child;
  @override
  Widget build(BuildContext context) => Padding(padding: const EdgeInsets.only(top: 16), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(title, style: Theme.of(context).textTheme.titleMedium), const SizedBox(height: 6), child]));
}
