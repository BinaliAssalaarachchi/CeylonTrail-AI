import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../config/api_config.dart';
import '../models/attraction_model.dart';
import '../models/travel_alert_model.dart';
import '../services/attraction_api_service.dart';
import '../services/api_client.dart';
import '../services/travel_alert_api_service.dart';
import '../theme/app_theme.dart';
import '../utils/attraction_images.dart';
import '../widgets/auth_scope.dart';

class AttractionDetailPage extends StatefulWidget {
  const AttractionDetailPage({required this.id, super.key});
  final String id;

  @override
  State<AttractionDetailPage> createState() => _AttractionDetailPageState();
}

class _AttractionDetailPageState extends State<AttractionDetailPage> {
  late AttractionApiService _service;
  late TravelAlertApiService _alertService;
  AttractionModel? _attraction;
  AvailabilityModel? _availability;
  List<TravelAlert> _districtAlerts = const [];
  bool _loading = true;
  bool _favoriteBusy = false;
  String? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final client = ApiClient(storage: AuthScope.of(context).storage);
    _service = AttractionApiService(client);
    _alertService = TravelAlertApiService(client);
    if (_attraction == null && _error == null) _load();
  }

  Future<void> _load() async {
    try {
      final attraction = await _service.getAttraction(widget.id);
      final availability = await _service.getAvailability(widget.id);
      final alertsPage = await _alertService
          .fetchAlerts(status: TravelAlertStatus.active, district: attraction.district)
          .catchError((_) => const TravelAlertPage(items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 1));

      final activeAlerts = alertsPage.items.where((alert) =>
        alert.endDateTime == null || alert.endDateTime!.isAfter(DateTime.now())
      ).toList();

      if (!mounted) return;
      setState(() {
        _attraction = attraction;
        _availability = availability;
        _districtAlerts = activeAlerts;
      });
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  static String _formatTime(DateTime? start, DateTime? end) {
    if (start == null && end == null) return '';
    final now = DateTime.now();
    final localStart = start?.toLocal();
    final localEnd = end?.toLocal();
    String fmt(DateTime dt) => '${dt.day.toString().padLeft(2, '0')}/${dt.month.toString().padLeft(2, '0')}/${dt.year} ${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
    if (localStart != null && localEnd != null) {
      final prefix = localStart.isAfter(now) ? 'Upcoming' : 'Active Now';
      return '$prefix · ${fmt(localStart)} – ${fmt(localEnd)}';
    } else if (localEnd != null) {
      return 'Until ${fmt(localEnd)}';
    } else {
      return 'From ${fmt(localStart!)}';
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
            _AttractionGallery(attraction: attraction),
            const SizedBox(height: 20),
            Text(attraction.name, style: Theme.of(context).textTheme.headlineMedium),
            const SizedBox(height: 8),
            Text('${attraction.category?.name ?? 'Experience'} · ${attraction.district}', style: Theme.of(context).textTheme.bodyLarge),
            const SizedBox(height: 8),
            Text(attraction.price == 0 ? 'Free entry' : 'From LKR ${attraction.price.toStringAsFixed(2)}', style: Theme.of(context).textTheme.titleMedium),
            if (_districtAlerts.isNotEmpty) ...[
              const SizedBox(height: 14),
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: Colors.amber.shade50,
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: Colors.amber.shade400, width: 1.5),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        const Icon(Icons.warning_rounded, color: Colors.orange, size: 20),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            'Active Travel Advisory in ${attraction.district}',
                            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13, color: Colors.black87),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    ..._districtAlerts.map((alert) => Container(
                      margin: const EdgeInsets.only(bottom: 6),
                      padding: const EdgeInsets.all(8),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(6),
                        border: Border.all(color: Colors.amber.shade200),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(alert.title, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13)),
                          const SizedBox(height: 3),
                          Text(
                            _formatTime(alert.startDateTime, alert.endDateTime),
                            style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: Colors.orange.shade900),
                          ),
                          const SizedBox(height: 4),
                          Text(alert.description, style: const TextStyle(fontSize: 12, color: Colors.black87)),
                        ],
                      ),
                    )),
                  ],
                ),
              ),
            ],
            const SizedBox(height: 18),
            Text(attraction.description, style: Theme.of(context).textTheme.bodyLarge),
            if (isTourist) ...[
              const SizedBox(height: 18),
              SizedBox(width: double.infinity, child: FilledButton.icon(onPressed: () => context.push('/discover/${attraction.id}/book'), icon: const Icon(Icons.event_available_outlined), label: const Text('Book Experience'))),
            ],
            const SizedBox(height: 18),
            _InfoSection(title: 'Address', child: Text(attraction.address)),
            _InfoSection(
              title: 'Availability',
              child: () {
                final futureSlots = (_availability?.slots ?? const [])
                    .where((slot) => slot.availableCapacity > 0 && _isSlotInFuture(slot))
                    .toList();
                if (futureSlots.isEmpty) {
                  return const Text('No upcoming availability for this experience.');
                }
                return Column(
                  children: futureSlots.map((slot) => ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: Text(slot.date.toLocal().toString().split(' ').first),
                    subtitle: Text('${_time(slot.startTime)} – ${_time(slot.endTime)}'),
                    trailing: Text('${slot.availableCapacity}/${slot.capacity} available'),
                  )).toList(),
                );
              }(),
            ),
          ],
        ),
      ),
    );
  }

  Widget _errorState() => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [const Text('Unable to load this attraction.'), const SizedBox(height: 12), FilledButton(onPressed: () { setState(() { _loading = true; _error = null; }); _load(); }, child: const Text('Retry'))]));
  static String _time(String? value) => value == null ? '' : value.substring(0, value.length >= 5 ? 5 : value.length);

  static bool _isSlotInFuture(ExperienceSlotModel slot) {
    final now = DateTime.now();
    final d = slot.date.toLocal();
    final parts = slot.endTime.split(':');
    final hour = parts.isNotEmpty ? (int.tryParse(parts[0]) ?? 23) : 23;
    final minute = parts.length > 1 ? (int.tryParse(parts[1]) ?? 59) : 59;
    final slotEnd = DateTime(d.year, d.month, d.day, hour, minute);
    return slotEnd.isAfter(now);
  }
}

class _AttractionGallery extends StatefulWidget {
  const _AttractionGallery({required this.attraction});

  final AttractionModel attraction;

  @override
  State<_AttractionGallery> createState() => _AttractionGalleryState();
}

class _AttractionGalleryState extends State<_AttractionGallery> {
  late final PageController _pageController;
  int _page = 0;

  @override
  void initState() {
    super.initState();
    _pageController = PageController();
  }

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final gallery = attractionGallery(widget.attraction);
    if (gallery.isEmpty) {
      return _GalleryPlaceholder(label: widget.attraction.district);
    }
    return ClipRRect(
      borderRadius: BorderRadius.circular(CeylonRadii.card),
      child: SizedBox(
        height: 230,
        child: Stack(
          fit: StackFit.expand,
          children: [
            PageView.builder(
              controller: _pageController,
              itemCount: gallery.length,
              onPageChanged: (page) => setState(() => _page = page),
              itemBuilder: (context, index) {
                final rawItem = gallery[index];
                final item = ApiConfig.resolveImageUrl(rawItem);
                return ColoredBox(
                  color: const Color(0xFF18231F),
                  child: Center(
                    child: item.isEmpty
                        ? const _GalleryPlaceholder(
                            label: 'Photo needed for this destination',
                          )
                        : item.startsWith('http')
                            ? Image.network(
                                item,
                                fit: BoxFit.cover,
                                width: double.infinity,
                                height: double.infinity,
                                errorBuilder: (_, __, ___) => const _GalleryPlaceholder(
                                  label: 'Photo needed for this destination',
                                ),
                              )
                            : Image.asset(
                                item,
                                fit: BoxFit.cover,
                                width: double.infinity,
                                height: double.infinity,
                                errorBuilder: (_, __, ___) => const _GalleryPlaceholder(
                                  label: 'Photo needed for this destination',
                                ),
                              ),
                  ),
                );
              },
            ),
            Positioned(
              left: 0,
              right: 0,
              bottom: 12,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: List.generate(
                  gallery.length,
                  (index) => AnimatedContainer(
                    duration: const Duration(milliseconds: 180),
                    width: index == _page ? 18 : 6,
                    height: 6,
                    margin: const EdgeInsets.symmetric(horizontal: 3),
                    decoration: BoxDecoration(
                      color: index == _page ? Colors.white : Colors.white60,
                      borderRadius: BorderRadius.circular(99),
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _GalleryPlaceholder extends StatelessWidget {
  const _GalleryPlaceholder({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) => Container(
    height: 230,
    decoration: BoxDecoration(color: CeylonColors.mint, borderRadius: BorderRadius.circular(CeylonRadii.card)),
    alignment: Alignment.center,
    child: Text(label, style: const TextStyle(color: CeylonColors.forest, fontWeight: FontWeight.w700)),
  );
}

class _InfoSection extends StatelessWidget {
  const _InfoSection({required this.title, required this.child});
  final String title;
  final Widget child;
  @override
  Widget build(BuildContext context) => Padding(padding: const EdgeInsets.only(top: 16), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(title, style: Theme.of(context).textTheme.titleMedium), const SizedBox(height: 6), child]));
}
