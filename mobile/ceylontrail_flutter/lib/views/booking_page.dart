import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/attraction_model.dart';
import '../models/booking_model.dart';
import '../models/trip_model.dart';
import '../services/attraction_api_service.dart';
import '../services/api_client.dart';
import '../services/booking_api_service.dart';
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
  AttractionModel? _attraction;
  List<AvailabilitySlotModel> _slots = const [];
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
      _bookingApi = BookingApiService(ApiClient(storage: AuthScope.of(context).storage));
      _attractionApi = AttractionApiService(ApiClient(storage: AuthScope.of(context).storage));
      _servicesReady = true;
      _load();
    }
  }

  Future<void> _load() async {
    try {
      final attractionFuture = _attractionApi.getAttraction(widget.attractionId);
      final slotsFuture = _bookingApi.fetchAvailabilitySlots(attractionId: widget.attractionId);
      final tripsFuture = widget.tripApi.getTrips().catchError((_) => <Trip>[]);

      final attraction = await attractionFuture;
      final slots = await slotsFuture;
      final trips = await tripsFuture;

      if (!mounted) return;
      setState(() {
        _attraction = attraction;
        _slots = slots.where((slot) => slot.availableCapacity > 0 && slot.endTime.isAfter(DateTime.now())).toList();
        _trips = trips;
        _loading = false;
      });
    } catch (error) {
      if (mounted) setState(() { _error = error.toString(); _loading = false; });
    }
  }

  Future<void> _submit() async {
    final slot = _selectedSlot;
    if (slot == null || _guests > slot.availableCapacity) return;
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
          const SizedBox(height: 12),
          SizedBox(height: 50, child: FilledButton(onPressed: _selectedSlot == null || _submitting ? null : _submit, child: Text(_submitting ? 'Confirming...' : 'Confirm Booking'))),
        ],
      ]),
    );
  }

  Widget _slotTile(AvailabilitySlotModel slot) => Card(child: RadioListTile<String>(value: slot.id, groupValue: _selectedSlot?.id, onChanged: (_) => setState(() { _selectedSlot = slot; if (_guests > slot.availableCapacity) _guests = slot.availableCapacity; }), title: Text(_slotText(slot)), subtitle: Text('${slot.availableCapacity} places left'), secondary: Text('LKR ${slot.pricePerPerson.toStringAsFixed(2)}')));

  Widget _successView(BookingModel booking) => Scaffold(appBar: AppBar(title: const Text('Booking Confirmed')), body: Padding(padding: const EdgeInsets.all(24), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [const Icon(Icons.check_circle, color: Colors.green, size: 64), const SizedBox(height: 16), Text('Your booking is confirmed.', style: Theme.of(context).textTheme.headlineSmall), const SizedBox(height: 12), Text('Reference: ${booking.id}'), Text('Status: ${booking.currentStatus}'), Text('Server total: LKR ${booking.totalAmount.toStringAsFixed(2)}'), const Spacer(), SizedBox(width: double.infinity, height: 50, child: FilledButton(onPressed: () => context.go('/bookings'), child: const Text('View My Bookings')))])));

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
  static String _date(DateTime date) => '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
  static String _slotText(AvailabilitySlotModel slot) => '${_date(slot.startTime)} · ${_time(slot.startTime)}–${_time(slot.endTime)}';
  static String _time(DateTime date) => '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
}
