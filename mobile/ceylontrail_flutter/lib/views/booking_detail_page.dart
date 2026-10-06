import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../config/api_config.dart';
import '../models/booking_model.dart';
import '../models/travel_alert_model.dart';
import '../services/booking_api_service.dart';
import '../theme/app_theme.dart';
import 'bookings_page.dart';

class BookingDetailPage extends StatefulWidget {
  const BookingDetailPage({required this.booking, required this.bookingService, required this.onBookingUpdated, super.key});

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

  Future<void> _showDeleteDraftDialog() async {
    final confirmed = await showDialog<bool>(
          context: context,
          builder: (dialogContext) => AlertDialog(
            title: const Text('Delete draft booking?'),
            content: const Text('Are you sure you want to remove this draft booking? This cannot be undone.'),
            actions: [
              TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Keep it')),
              FilledButton(
                style: FilledButton.styleFrom(backgroundColor: CeylonColors.error),
                onPressed: () => Navigator.of(dialogContext).pop(true),
                child: const Text('Delete'),
              ),
            ],
          ),
        ) ??
        false;
    if (!confirmed || !mounted) return;
    setState(() => _isDeleting = true);
    try {
      await widget.bookingService.deleteBooking(_currentBooking.id);
      widget.onBookingUpdated();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Draft booking deleted.')));
      Navigator.of(context).pop();
    } catch (error) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.toString())));
    } finally {
      if (mounted) setState(() => _isDeleting = false);
    }
  }

  Future<void> _showCancelDialog() async {
    final controller = TextEditingController();
    final confirmed = await showDialog<bool>(
          context: context,
          builder: (dialogContext) => AlertDialog(
            title: const Text('Cancel booking'),
            content: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('Please state the reason for cancellation.'),
                const SizedBox(height: 12),
                TextField(
                  controller: controller,
                  maxLines: 3,
                  decoration: const InputDecoration(hintText: 'For example, a change of plans', border: OutlineInputBorder()),
                ),
              ],
            ),
            actions: [
              TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Keep booking')),
              FilledButton(
                style: FilledButton.styleFrom(backgroundColor: CeylonColors.error),
                onPressed: () {
                  if (controller.text.trim().isNotEmpty) {
                    Navigator.of(dialogContext).pop(true);
                  }
                },
                child: const Text('Confirm cancellation'),
              ),
            ],
          ),
        ) ??
        false;
    if (!confirmed || !mounted) return;
    setState(() => _isCancelling = true);
    try {
      final updated = await widget.bookingService.cancelBooking(_currentBooking.id, controller.text.trim());
      if (!mounted) return;
      setState(() => _currentBooking = updated);
      widget.onBookingUpdated();
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Booking successfully cancelled.')));
    } catch (error) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.toString())));
    } finally {
      if (mounted) setState(() => _isCancelling = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final status = bookingStatusPresentation(_currentBooking.status);
    final canCancel = const {'PendingAI', 'PendingHumanApproval', 'Confirmed'}.contains(_currentBooking.status);
    final isDraft = _currentBooking.status == 'Draft';
    final advisories = _currentBooking.activeAdvisories.where((item) => item.endDateTime == null || item.endDateTime!.isAfter(DateTime.now())).toList();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Booking details'),
        actions: [
          if (isDraft)
            IconButton(tooltip: 'Delete draft booking', onPressed: _isDeleting ? null : _showDeleteDraftDialog, icon: const Icon(Icons.delete_outline)),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 760),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            _DetailVisual(
                              imageUrl: _currentBooking.items
                                  .where((i) => i.attractionImageUrl != null && i.attractionImageUrl!.isNotEmpty)
                                  .firstOrNull
                                  ?.attractionImageUrl,
                            ),
                            const SizedBox(width: 14),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    _currentBooking.displayTitle,
                                    style: Theme.of(context).textTheme.headlineMedium,
                                  ),
                                  const SizedBox(height: 10),
                                  _StatusChip(status: status),
                                ],
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 24),
                        const Divider(),
                        const SizedBox(height: 18),
                        Text('Reservation details', style: Theme.of(context).textTheme.titleMedium),
                        const SizedBox(height: 14),
                        _DetailLine(icon: Icons.people_outline, label: 'Guests', value: _guestLabel(_currentBooking)),
                        const SizedBox(height: 12),
                        _DetailLine(icon: Icons.payments_outlined, label: 'Total', value: formatLkr(_currentBooking.totalAmount), emphasize: true),
                        const SizedBox(height: 12),
                        _ReferenceLine(reference: _currentBooking.id),
                      ],
                    ),
                  ),
                ),
                if (advisories.isNotEmpty) ...[const SizedBox(height: 16), _AdvisoryCard(advisories: advisories)],
                const SizedBox(height: 24),
                Text('Your booking', style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: 10),
                ..._currentBooking.items.asMap().entries.map((entry) => _BookingItemCard(item: entry.value, index: entry.key)),
                if (_currentBooking.items.isEmpty) const _EmptyItemCard(),
                if (_currentBooking.statusHistory.isNotEmpty) ...[
                  const SizedBox(height: 24),
                  Text('Booking progress', style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 10),
                  _HistoryTimeline(history: _currentBooking.statusHistory),
                ],
                if (_currentBooking.cancellation != null) ...[
                  const SizedBox(height: 16),
                  _CancellationCard(cancellation: _currentBooking.cancellation!),
                ],
                const SizedBox(height: 24),
                if (canCancel)
                  SizedBox(
                    width: double.infinity,
                    child: OutlinedButton.icon(
                      style: OutlinedButton.styleFrom(foregroundColor: CeylonColors.error, side: const BorderSide(color: CeylonColors.error)),
                      onPressed: _isCancelling ? null : _showCancelDialog,
                      icon: const Icon(Icons.cancel_outlined),
                      label: Text(_isCancelling ? 'Processing...' : 'Cancel booking'),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

String _guestLabel(BookingModel booking) {
  final guests = booking.items.fold<int>(0, (sum, item) => sum + item.numberOfGuests);
  return '$guests ${guests == 1 ? 'guest' : 'guests'}';
}

class _DetailVisual extends StatelessWidget {
  const _DetailVisual({this.imageUrl});

  final String? imageUrl;

  @override
  Widget build(BuildContext context) {
    final resolvedUrl = ApiConfig.resolveImageUrl(imageUrl);
    if (resolvedUrl.isNotEmpty) {
      return ClipRRect(
        borderRadius: BorderRadius.circular(16),
        child: resolvedUrl.startsWith('http')
            ? Image.network(
                resolvedUrl,
                width: 54,
                height: 54,
                fit: BoxFit.cover,
                errorBuilder: (context, error, stackTrace) => Container(
                  width: 54,
                  height: 54,
                  decoration: BoxDecoration(color: CeylonColors.mint, borderRadius: BorderRadius.circular(16)),
                  child: const Icon(Icons.confirmation_number_outlined, color: CeylonColors.forest, size: 27),
                ),
              )
            : Image.asset(
                resolvedUrl,
                width: 54,
                height: 54,
                fit: BoxFit.cover,
                errorBuilder: (context, error, stackTrace) => Container(
                  width: 54,
                  height: 54,
                  decoration: BoxDecoration(color: CeylonColors.mint, borderRadius: BorderRadius.circular(16)),
                  child: const Icon(Icons.confirmation_number_outlined, color: CeylonColors.forest, size: 27),
                ),
              ),
      );
    }
    return Container(
      width: 54,
      height: 54,
      decoration: BoxDecoration(color: CeylonColors.mint, borderRadius: BorderRadius.circular(16)),
      child: const Icon(Icons.confirmation_number_outlined, color: CeylonColors.forest, size: 27),
    );
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.status});

  final BookingStatusPresentation status;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(color: status.background, borderRadius: BorderRadius.circular(99)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(status.icon, size: 16, color: status.foreground),
          const SizedBox(width: 6),
          Text(status.label, style: TextStyle(color: status.foreground, fontSize: 12, fontWeight: FontWeight.w800)),
        ],
      ),
    );
  }
}

class _DetailLine extends StatelessWidget {
  const _DetailLine({required this.icon, required this.label, required this.value, this.emphasize = false});

  final IconData icon;
  final String label;
  final String value;
  final bool emphasize;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, size: 19, color: CeylonColors.inkMuted),
        const SizedBox(width: 10),
        Expanded(child: Text(label)),
        Text(value, style: TextStyle(color: emphasize ? CeylonColors.forest : CeylonColors.ink, fontWeight: emphasize ? FontWeight.w800 : FontWeight.w600)),
      ],
    );
  }
}

class _ReferenceLine extends StatelessWidget {
  const _ReferenceLine({required this.reference});

  final String reference;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        const Icon(Icons.receipt_long_outlined, size: 19, color: CeylonColors.inkMuted),
        const SizedBox(width: 10),
        const Expanded(child: Text('Booking reference')),
        Flexible(
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Flexible(child: SelectableText(reference, textAlign: TextAlign.end, style: const TextStyle(fontSize: 12))),
              IconButton(
                tooltip: 'Copy booking reference',
                visualDensity: VisualDensity.compact,
                onPressed: () async {
                  await Clipboard.setData(ClipboardData(text: reference));
                  if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Reference copied.')));
                },
                icon: const Icon(Icons.copy_outlined, size: 17),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _BookingItemCard extends StatelessWidget {
  const _BookingItemCard({required this.item, required this.index});

  final BookingItemModel item;
  final int index;

  @override
  Widget build(BuildContext context) {
    final title = (item.attractionName != null && item.attractionName!.isNotEmpty)
        ? item.attractionName!
        : 'Experience ${index + 1}';

    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            if (item.district != null && item.district!.isNotEmpty) ...[
              const SizedBox(height: 4),
              Row(
                children: [
                  const Icon(Icons.location_on_outlined, size: 16, color: CeylonColors.inkMuted),
                  const SizedBox(width: 4),
                  Text(item.district!, style: Theme.of(context).textTheme.bodySmall?.copyWith(color: CeylonColors.inkMuted)),
                ],
              ),
            ],
            if (item.slotStartTime != null) ...[
              const SizedBox(height: 4),
              Row(
                children: [
                  const Icon(Icons.schedule_outlined, size: 16, color: CeylonColors.inkMuted),
                  const SizedBox(width: 4),
                  Text(_formatDate(item.slotStartTime!), style: Theme.of(context).textTheme.bodySmall?.copyWith(color: CeylonColors.inkMuted)),
                ],
              ),
            ],
            const SizedBox(height: 10),
            Text('${item.numberOfGuests} ${item.numberOfGuests == 1 ? 'guest' : 'guests'} × ${formatLkr(item.unitPrice)}'),
            const SizedBox(height: 10),
            Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
              const Text('Subtotal'),
              Text(formatLkr(item.subtotal), style: const TextStyle(fontWeight: FontWeight.w800)),
            ]),
          ],
        ),
      ),
    );
  }
}

class _EmptyItemCard extends StatelessWidget {
  const _EmptyItemCard();

  @override
  Widget build(BuildContext context) {
    return Card(child: Padding(padding: const EdgeInsets.all(16), child: Text('Item details are not available for this reservation.', style: Theme.of(context).textTheme.bodyMedium)));
  }
}

class _HistoryTimeline extends StatelessWidget {
  const _HistoryTimeline({required this.history});

  final List<BookingHistoryModel> history;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(children: [
          for (var index = 0; index < history.length; index++)
            _HistoryEntry(entry: history[index], isLast: index == history.length - 1),
        ]),
      ),
    );
  }
}

class _HistoryEntry extends StatelessWidget {
  const _HistoryEntry({required this.entry, required this.isLast});

  final BookingHistoryModel entry;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    final presentation = bookingStatusPresentation(entry.newStatus);
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Column(children: [
          Icon(Icons.check_circle, size: 18, color: presentation.foreground),
          if (!isLast) Container(width: 1, height: 34, color: CeylonColors.outline),
        ]),
        const SizedBox(width: 12),
        Expanded(
          child: Padding(
            padding: const EdgeInsets.only(bottom: 14),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(_historyLabel(entry.newStatus), style: const TextStyle(fontWeight: FontWeight.w700)),
              const SizedBox(height: 3),
              Text(_formatDate(entry.timestamp), style: Theme.of(context).textTheme.bodySmall),
            ]),
          ),
        ),
      ],
    );
  }
}

String _historyLabel(String status) {
  switch (status) {
    case 'Draft': return 'Reservation created';
    case 'Confirmed': return 'Booking confirmed';
    case 'Cancelled': return 'Booking cancelled';
    case 'Rejected': return 'Booking not confirmed';
    case 'Completed': return 'Experience completed';
    case 'PendingAI':
    case 'PendingHumanApproval': return 'Confirmation requested';
    default: return 'Booking updated';
  }
}

String _formatDate(DateTime date) {
  final local = date.toLocal();
  return '${local.day.toString().padLeft(2, '0')}/${local.month.toString().padLeft(2, '0')}/${local.year} · ${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
}

class _CancellationCard extends StatelessWidget {
  const _CancellationCard({required this.cancellation});

  final CancellationModel cancellation;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: const Color(0xFFFBE8E8),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Cancellation note', style: TextStyle(color: CeylonColors.error, fontWeight: FontWeight.w800)),
          const SizedBox(height: 6),
          Text(cancellation.reason, style: const TextStyle(color: CeylonColors.error)),
        ]),
      ),
    );
  }
}

class _AdvisoryCard extends StatelessWidget {
  const _AdvisoryCard({required this.advisories});

  final List<TravelAlert> advisories;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: const Color(0xFFFFF6E4),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Row(children: [
            Icon(Icons.warning_amber_rounded, color: Color(0xFF8A5A18)),
            SizedBox(width: 8),
            Text('Travel advisory', style: TextStyle(fontWeight: FontWeight.w800)),
          ]),
          const SizedBox(height: 10),
          for (final advisory in advisories) ...[
            Text(advisory.title, style: const TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 4),
            Text(advisory.description),
            if (advisory != advisories.last) const SizedBox(height: 12),
          ],
        ]),
      ),
    );
  }
}
