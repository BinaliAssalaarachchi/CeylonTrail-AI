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
  bool _isDeleting = false;

  @override
  void initState() {
    super.initState();
    _currentBooking = widget.booking;
  }

  Color _getStatusColor(String status) {
    switch (status.toUpperCase()) {
      case 'DRAFT':
        return Colors.brown.shade400;
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

  Future<void> _showDeleteDraftDialog() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete Draft Booking?'),
        content: const Text('Are you sure you want to delete this draft booking? This will remove it from both your bookings and provider records.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: Colors.red),
            onPressed: () => Navigator.of(ctx).pop(true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );

    if (confirmed == true && mounted) {
      setState(() => _isDeleting = true);
      try {
        await widget.bookingService.deleteBooking(_currentBooking.id);
        widget.onBookingUpdated();
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Draft booking deleted.')),
          );
          Navigator.of(context).pop();
        }
      } catch (err) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(err.toString())),
          );
        }
      } finally {
        if (mounted) setState(() => _isDeleting = false);
      }
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
    final now = DateTime.now();
    final activeAdvisories = _currentBooking.activeAdvisories
        .where((a) => a.endDateTime == null || a.endDateTime!.isAfter(now))
        .toList();

    final isDraft = _currentBooking.status == 'Draft';

    return Scaffold(
      appBar: AppBar(
        title: const Text('Reservation Details'),
        actions: [
          if (isDraft)
            IconButton(
              icon: const Icon(Icons.delete_outline, color: Colors.red),
              tooltip: 'Delete Draft Booking',
              onPressed: _isDeleting ? null : _showDeleteDraftDialog,
            ),
        ],
      ),
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
            // Active Travel Advisory & Current Situation Panel
            if (activeAdvisories.isNotEmpty) ...[
              Card(
                color: Colors.amber.shade50,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                  side: BorderSide(color: Colors.amber.shade400, width: 1.5),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          const Icon(Icons.warning_rounded, color: Colors.orange, size: 22),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              'Destination Advisory (${activeAdvisories.length})',
                              style: const TextStyle(
                                fontWeight: FontWeight.bold,
                                fontSize: 15,
                                color: Colors.black87,
                              ),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 8),
                      const Text(
                        'Current situation in the destination district for this booking:',
                        style: TextStyle(fontSize: 12, color: Colors.black54),
                      ),
                      const SizedBox(height: 10),
                      ...activeAdvisories.map((advisory) => Container(
                        margin: const EdgeInsets.only(bottom: 8),
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(8),
                          border: Border.all(color: Colors.amber.shade200),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Expanded(
                                  child: Text(
                                    advisory.title,
                                    style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
                                  ),
                                ),
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                                  decoration: BoxDecoration(
                                    color: Colors.orange.shade100,
                                    borderRadius: BorderRadius.circular(4),
                                  ),
                                  child: Text(
                                    advisory.district,
                                    style: const TextStyle(fontSize: 11, fontWeight: FontWeight.bold, color: Colors.orange),
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 4),
                            Text(
                              _formatAlertTime(advisory.startDateTime, advisory.endDateTime),
                              style: TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w600,
                                color: Colors.orange.shade900,
                              ),
                            ),
                            const SizedBox(height: 6),
                            Text(advisory.description, style: const TextStyle(fontSize: 12, color: Colors.black87)),
                            if (advisory.source != null && advisory.source!.isNotEmpty) ...[
                              const SizedBox(height: 6),
                              Text(
                                'Source: ${advisory.source}',
                                style: TextStyle(fontSize: 11, color: Colors.grey.shade600, fontStyle: FontStyle.italic),
                              ),
                            ],
                          ],
                        ),
                      )),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 16),
            ],

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

            if (isDraft)
              SizedBox(
                width: double.infinity,
                height: 48,
                child: OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(foregroundColor: Colors.red),
                  onPressed: _isDeleting ? null : _showDeleteDraftDialog,
                  icon: const Icon(Icons.delete_outline),
                  label: Text(_isDeleting ? 'Deleting...' : 'Delete Draft Booking'),
                ),
              ),

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
}
