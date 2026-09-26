import 'package:flutter/material.dart';
import '../models/booking_model.dart';
import '../services/booking_api_service.dart';

class BookingDetailPage extends StatefulWidget {
  const BookingDetailPage({
    required this.booking,
    required this.bookingService,
    required this.onBookingUpdated,
    super.key,
  });

  final BookingModel booking;
  final BookingApiService bookingService;
  final VoidCallback onBookingUpdated;

  @override
  State<BookingDetailPage> createState() => _BookingDetailPageState();
}

class _BookingDetailPageState extends State<BookingDetailPage> {
  late BookingModel _currentBooking;
  bool _isCancelling = false;

  @override
  void initState() {
    super.initState();
    _currentBooking = widget.booking;
  }

  Color _getStatusColor(String status) {
    switch (status.toUpperCase()) {
      case 'PENDING':
        return Colors.orange;
      case 'CONFIRMED':
        return Colors.green;
      case 'REJECTED':
      case 'CANCELLED':
        return Colors.red;
      default:
        return Colors.grey;
    }
  }

  Future<void> _showCancelDialog() async {
    final controller = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Cancel Booking'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Please state the reason for cancellation:'),
            const SizedBox(height: 12),
            TextField(
              controller: controller,
              maxLines: 3,
              decoration: const InputDecoration(
                hintText: 'e.g. Flight rescheduling or change of plans...',
                border: OutlineInputBorder(),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text('Keep Booking'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: Colors.red),
            onPressed: () {
              if (controller.text.trim().isNotEmpty) {
                Navigator.of(ctx).pop(true);
              }
            },
            child: const Text('Confirm Cancel'),
          ),
        ],
      ),
    );

    if (confirmed == true && mounted) {
      setState(() => _isCancelling = true);
      try {
        final updated = await widget.bookingService.cancelBooking(
          _currentBooking.id,
          controller.text.trim(),
        );
        setState(() => _currentBooking = updated);
        widget.onBookingUpdated();
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Booking successfully cancelled.')),
          );
        }
      } catch (err) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(err.toString())),
          );
        }
      } finally {
        if (mounted) setState(() => _isCancelling = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final canCancel = _currentBooking.status == 'Pending' ||
        _currentBooking.status == 'Confirmed';

    return Scaffold(
      appBar: AppBar(title: const Text('Reservation Details')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Header card with Status & Reference
            Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Chip(
                          avatar: CircleAvatar(
                            backgroundColor: _getStatusColor(_currentBooking.status),
                            radius: 5,
                          ),
                          label: Text(
                            _currentBooking.status,
                            style: TextStyle(
                              color: _getStatusColor(_currentBooking.status),
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                        Text(
                          'LKR ${_currentBooking.totalAmount.toStringAsFixed(2)}',
                          style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                                fontWeight: FontWeight.bold,
                                color: Theme.of(context).colorScheme.primary,
                              ),
                        ),
                      ],
                    ),
                    const Divider(height: 24),
                    Text(
                      'Booking Reference:',
                      style: Theme.of(context).textTheme.labelSmall,
                    ),
                    SelectableText(
                      _currentBooking.id,
                      style: const TextStyle(fontFamily: 'monospace', fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Booked on: ${_currentBooking.createdAt.toLocal().toString().substring(0, 16)}',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 20),

            // Itemized line items
            Text('Reserved Items', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 10),
            ..._currentBooking.items.map(
              (item) => Card(
                margin: const EdgeInsets.only(bottom: 10),
                child: ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.confirmation_number_outlined)),
                  title: Text('Availability slot (${item.availabilitySlotId.substring(0, item.availabilitySlotId.length > 8 ? 8 : item.availabilitySlotId.length)}...)'),
                  subtitle: Text('Guests: ${item.numberOfGuests} × LKR ${item.unitPrice.toStringAsFixed(2)}'),
                  trailing: Text(
                    'LKR ${item.subtotal.toStringAsFixed(2)}',
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  ),
                ),
              ),
            ),

            if (_currentBooking.cancellation != null) ...[
              const SizedBox(height: 16),
              Card(
                color: Colors.red.shade50,
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Cancellation Reason:',
                        style: TextStyle(color: Colors.red.shade900, fontWeight: FontWeight.bold),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        _currentBooking.cancellation!.reason,
                        style: TextStyle(color: Colors.red.shade800),
                      ),
                    ],
                  ),
                ),
              ),
            ],

            const SizedBox(height: 24),

            if (canCancel)
              SizedBox(
                width: double.infinity,
                height: 48,
                child: OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(foregroundColor: Colors.red),
                  onPressed: _isCancelling ? null : _showCancelDialog,
                  icon: const Icon(Icons.cancel_outlined),
                  label: Text(_isCancelling ? 'Processing...' : 'Cancel Reservation'),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
