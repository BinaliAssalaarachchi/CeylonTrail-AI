import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/attraction_model.dart';
import '../models/booking_model.dart';
import '../models/travel_alert_model.dart';
import '../models/trip_model.dart';
import '../services/attraction_api_service.dart';
import '../services/api_client.dart';
import '../services/booking_api_service.dart';
import '../services/travel_alert_api_service.dart';
import '../services/trip_api_service.dart';
import '../widgets/auth_scope.dart';

class BookingPage extends StatefulWidget {
  const BookingPage({required this.attractionId, required this.tripApi, super.key});
  final String attractionId;
  final TripApiService tripApi;

  @override
  State<BookingPage> createState() => _BookingPageState();
}

class _BookingPageState extends State<BookingPage> {
  late final BookingApiService _bookingApi;
  late final AttractionApiService _attractionApi;
  late final TravelAlertApiService _alertApi;
  AttractionModel? _attraction;
  List<AvailabilitySlotModel> _slots = const [];
  List<TravelAlert> _alerts = const [];
  List<Trip> _trips = const [];
  AvailabilitySlotModel? _selectedSlot;
  String? _selectedTripId;
  BookingModel? _booking;
  int _guests = 1;
  bool _loading = true;
  bool _submitting = false;
  bool _servicesReady = false;
  String? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_servicesReady) {
      final client = ApiClient(storage: AuthScope.of(context).storage);
      _bookingApi = BookingApiService(client);
      _attractionApi = AttractionApiService(client);
      _alertApi = TravelAlertApiService(client);
      _servicesReady = true;
      _load();
    }
  }

  Future<void> _load() async {
    try {
      final attraction = await _attractionApi.getAttraction(widget.attractionId);
      final slotsFuture = _bookingApi.fetchAvailabilitySlots(attractionId: widget.attractionId);
      final tripsFuture = widget.tripApi.getTrips().catchError((_) => <Trip>[]);
      final alertsFuture = _alertApi
          .fetchAlerts(status: TravelAlertStatus.active, district: attraction.district)
          .catchError((_) => const TravelAlertPage(items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 1));

      final slots = await slotsFuture;
      final trips = await tripsFuture;
      final alertsPage = await alertsFuture;

      if (!mounted) return;
      final now = DateTime.now();
      setState(() {
        _attraction = attraction;
        _slots = slots.where((slot) => slot.availableCapacity > 0 && slot.endTime.isAfter(now)).toList();
        _trips = trips;
        _alerts = alertsPage.items.where((alert) => alert.endDateTime == null || alert.endDateTime!.isAfter(now)).toList();
        _loading = false;
      });
    } catch (error) {
      if (mounted) setState(() { _error = error.toString(); _loading = false; });
    }
  }

  List<TravelAlert> _advisoriesForSlot(AvailabilitySlotModel slot) {
    final district = _attraction?.district.trim().toLowerCase();
    if (district == null || district.isEmpty) return const [];
    final slotStart = slot.startTime.toLocal();
    final slotEnd = slot.endTime.toLocal();

    return _alerts.where((alert) {
      if (alert.district.trim().toLowerCase() != district) return false;

      final alertStart = alert.startDateTime?.toLocal();
      final alertEnd = alert.endDateTime?.toLocal();

      final startsBeforeSlotEnds = alertStart == null || !alertStart.isAfter(slotEnd);
      final endsAfterSlotStarts = alertEnd == null || !alertEnd.isBefore(slotStart);

      return startsBeforeSlotEnds && endsAfterSlotStarts;
    }).toList();
  }

  List<TravelAlert> get _advisoriesForSelectedSlot {
    final slot = _selectedSlot;
    if (slot == null) return const [];
    return _advisoriesForSlot(slot);
  }

  Future<void> _confirmAndSubmit() async {
    final slot = _selectedSlot;
    if (slot == null || _guests > slot.availableCapacity) return;

    final matchingAdvisories = _advisoriesForSelectedSlot;
    if (matchingAdvisories.isNotEmpty) {
      final shouldProceed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Row(
            children: const [
              Icon(Icons.warning_amber_rounded, color: Colors.orange),
              SizedBox(width: 8),
              Expanded(child: Text('Travel Advisory Notice')),
            ],
          ),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'There is an active travel advisory in ${_attraction?.district ?? "this area"} for your selected date/time:',
                  style: const TextStyle(fontSize: 14),
                ),
                const SizedBox(height: 10),
                ...matchingAdvisories.map((advisory) => Container(
                  margin: const EdgeInsets.only(bottom: 8),
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: Colors.amber.shade50,
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(color: Colors.amber.shade300),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(advisory.title, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13)),
                      const SizedBox(height: 3),
                      Text(
                        _formatAlertTime(advisory.startDateTime, advisory.endDateTime),
                        style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: Colors.orange.shade900),
                      ),
                      const SizedBox(height: 4),
                      Text(advisory.description, style: const TextStyle(fontSize: 12)),
                      if (advisory.source != null && advisory.source!.isNotEmpty) ...[
                        const SizedBox(height: 4),
                        Text('Source: ${advisory.source}', style: TextStyle(fontSize: 11, color: Colors.grey.shade700, fontStyle: FontStyle.italic)),
                      ],
                    ],
                  ),
                )),
                const SizedBox(height: 4),
                const Text('Do you wish to acknowledge this advisory and proceed with your booking?'),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(ctx).pop(false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.of(ctx).pop(true),
              child: const Text('Proceed with Booking'),
            ),
          ],
        ),
      );

      if (shouldProceed != true) return;
    }

    setState(() => _submitting = true);
    try {
      final booking = await _bookingApi.createBooking(tripId: _selectedTripId, availabilitySlotId: slot.id, numberOfGuests: _guests);
      if (mounted) setState(() { _booking = booking; _submitting = false; });
    } catch (error) {
      if (mounted) {
        setState(() => _submitting = false);
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.toString())));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Scaffold(body: Center(child: CircularProgressIndicator()));
    if (_error != null) return Scaffold(appBar: AppBar(title: const Text('Book Experience')), body: _errorView());
    if (_booking != null) return _successView(_booking!);
    final attraction = _attraction;
    final advisories = _advisoriesForSelectedSlot;

    return Scaffold(
      appBar: AppBar(title: const Text('Book Experience')),
      body: ListView(padding: const EdgeInsets.all(20), children: [
        Text(attraction?.name ?? 'Experience', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 6),
        const Text('Choose a live availability slot. Prices and capacity come from the server.'),
        const SizedBox(height: 20),
        Text('Available slots', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 8),
        if (_slots.isEmpty) const Card(child: Padding(padding: EdgeInsets.all(16), child: Text('No bookable slots are currently available for this attraction.'))),
        ..._slots.map(_slotTile),
        if (_slots.isNotEmpty) ...[
          const SizedBox(height: 18),
          Text('Guests', style: Theme.of(context).textTheme.titleLarge),
          Row(children: [
            IconButton(onPressed: _guests > 1 ? () => setState(() => _guests--) : null, icon: const Icon(Icons.remove_circle_outline)),
            Text('$_guests', style: Theme.of(context).textTheme.titleMedium),
            IconButton(onPressed: _selectedSlot != null && _guests < _selectedSlot!.availableCapacity ? () => setState(() => _guests++) : null, icon: const Icon(Icons.add_circle_outline)),
            const Text(' person(s)'),
          ]),
          const SizedBox(height: 12),
          Text('Add to a trip (optional)', style: Theme.of(context).textTheme.titleLarge),
          DropdownButtonFormField<String?>(
            value: _selectedTripId,
            decoration: const InputDecoration(border: OutlineInputBorder(), labelText: 'Trip'),
            items: [const DropdownMenuItem<String?>(value: null, child: Text('No trip / book separately')), ..._trips.map((trip) => DropdownMenuItem<String?>(value: trip.id, child: Text('${trip.name} · ${_date(trip.startDate)}')))],
            onChanged: (value) => setState(() => _selectedTripId = value),
          ),
          const SizedBox(height: 18),
          if (_selectedSlot != null) Card(child: ListTile(title: const Text('Review'), subtitle: Text('${_slotText(_selectedSlot!)}\n$_guests guest(s)'), trailing: Text('LKR ${( _selectedSlot!.pricePerPerson * _guests).toStringAsFixed(2)}'))),
          if (advisories.isNotEmpty) ...[
            const SizedBox(height: 12),
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
                      const SizedBox(width: 6),
                      Text(
                        'Travel Advisory for this session (${advisories.length})',
                        style: const TextStyle(fontWeight: FontWeight.bold, color: Colors.black87, fontSize: 13),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  ...advisories.map((advisory) => Padding(
                    padding: const EdgeInsets.only(bottom: 6),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          '• ${advisory.title}',
                          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 12, color: Colors.black87),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          _formatAlertTime(advisory.startDateTime, advisory.endDateTime),
                          style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: Colors.orange.shade900),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          advisory.description,
                          style: const TextStyle(fontSize: 12, color: Colors.black87),
                        ),
                      ],
                    ),
                  )),
                ],
              ),
            ),
          ],
          const SizedBox(height: 14),
          SizedBox(height: 50, child: FilledButton(onPressed: _selectedSlot == null || _submitting ? null : _confirmAndSubmit, child: Text(_submitting ? 'Confirming...' : 'Confirm Booking'))),
        ],
      ]),
    );
  }

  Widget _slotTile(AvailabilitySlotModel slot) {
    final slotAdvisories = _advisoriesForSlot(slot);

    return Card(
      child: RadioListTile<String>(
        value: slot.id,
        groupValue: _selectedSlot?.id,
        onChanged: (_) => setState(() {
          _selectedSlot = slot;
          if (_guests > slot.availableCapacity) _guests = slot.availableCapacity;
        }),
        title: Row(
          children: [
            Expanded(child: Text(_slotText(slot))),
            if (slotAdvisories.isNotEmpty)
              Container(
                margin: const EdgeInsets.only(left: 4),
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                decoration: BoxDecoration(
                  color: Colors.orange.shade100,
                  borderRadius: BorderRadius.circular(4),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: const [
                    Icon(Icons.warning_amber_rounded, size: 14, color: Colors.orange),
                    SizedBox(width: 2),
                    Text('Advisory', style: TextStyle(fontSize: 10, color: Colors.orange, fontWeight: FontWeight.bold)),
                  ],
                ),
              ),
          ],
        ),
        subtitle: Text('${slot.availableCapacity} places left'),
        secondary: Text('LKR ${slot.pricePerPerson.toStringAsFixed(2)}'),
      ),
    );
  }

  Widget _successView(BookingModel booking) {
    final now = DateTime.now();
    final slot = _selectedSlot;
    final fallbackList = slot != null ? _advisoriesForSlot(slot) : const <TravelAlert>[];
    final rawList = booking.activeAdvisories.isNotEmpty ? booking.activeAdvisories : fallbackList;
    final list = rawList.where((alert) => alert.endDateTime == null || alert.endDateTime!.isAfter(now)).toList();
    return Scaffold(
      appBar: AppBar(
        title: const Text('Booking Confirmed'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/discover'),
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Icon(Icons.check_circle, color: Colors.green, size: 64),
            const SizedBox(height: 16),
            Text('Your booking is applied & confirmed.', style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 12),
            Text('Reference: ${booking.id}'),
            Text('Status: ${booking.currentStatus}'),
            Text('Server total: LKR ${booking.totalAmount.toStringAsFixed(2)}'),
            if (list.isNotEmpty) ...[
              const SizedBox(height: 20),
              Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: Colors.amber.shade50,
                  borderRadius: BorderRadius.circular(12),
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
                            '⚠️ Destination Advisory & Current Situation in ${_attraction?.district ?? "the area"}',
                            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: Colors.black87),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    const Text(
                      'Please review the current situation before travelling to your booked destination:',
                      style: TextStyle(fontSize: 12, color: Colors.black54),
                    ),
                    const SizedBox(height: 10),
                    ...list.map((advisory) => Container(
                      margin: const EdgeInsets.only(bottom: 8),
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(color: Colors.amber.shade200),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(advisory.title, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13)),
                          const SizedBox(height: 3),
                          Text(
                            _formatAlertTime(advisory.startDateTime, advisory.endDateTime),
                            style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: Colors.orange.shade900),
                          ),
                          const SizedBox(height: 4),
                          Text(advisory.description, style: const TextStyle(fontSize: 12, color: Colors.black87)),
                          if (advisory.source != null && advisory.source!.isNotEmpty) ...[
                            const SizedBox(height: 4),
                            Text('Source: ${advisory.source}', style: TextStyle(fontSize: 11, color: Colors.grey.shade600, fontStyle: FontStyle.italic)),
                          ],
                        ],
                      ),
                    )),
                  ],
                ),
              ),
            ],
            const SizedBox(height: 24),
            SizedBox(
              width: double.infinity,
              height: 50,
              child: FilledButton.icon(
                icon: const Icon(Icons.explore),
                onPressed: () => context.go('/discover'),
                label: const Text('Back to Discover'),
              ),
            ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              height: 50,
              child: OutlinedButton(
                onPressed: () => context.go('/bookings'),
                child: const Text('View My Bookings'),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _errorView() => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text('Unable to load booking information.'),
          if (_error != null && _error!.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(
              _error!,
              style: const TextStyle(color: Colors.red, fontSize: 13),
              textAlign: TextAlign.center,
            ),
          ],
          const SizedBox(height: 12),
          FilledButton(
            onPressed: () {
              setState(() {
                _loading = true;
                _error = null;
              });
              _load();
            },
            child: const Text('Retry'),
          ),
        ],
      ),
    ),
  );

  static String _formatAlertTime(DateTime? start, DateTime? end) {
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

  static String _date(DateTime date) => '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
  static String _slotText(AvailabilitySlotModel slot) => '${_date(slot.startTime)} · ${_time(slot.startTime)}–${_time(slot.endTime)}';
  static String _time(DateTime date) => '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
}

