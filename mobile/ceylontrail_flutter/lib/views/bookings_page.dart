import 'package:flutter/material.dart';
import '../models/booking_model.dart';
import '../services/api_client.dart';
import '../services/booking_api_service.dart';
import '../widgets/auth_scope.dart';
import 'booking_detail_page.dart';

List<BookingModel> filterBookingsByTab(List<BookingModel> bookings, int tabIndex) {
  if (tabIndex == 0) {
    return bookings
        .where((booking) => const {
              'Draft',
              'PendingAI',
              'PendingHumanApproval',
              'Confirmed',
            }.contains(booking.status))
        .toList();
  } else if (tabIndex == 1) {
    return bookings.where((booking) => booking.status == 'Confirmed').toList();
  } else {
    return bookings
        .where((booking) => booking.status == 'Cancelled' ||
            booking.status == 'Rejected' ||
            booking.status == 'Completed')
        .toList();
  }
}

class BookingsPage extends StatefulWidget {
  const BookingsPage({super.key});

  @override
  State<BookingsPage> createState() => _BookingsPageState();
}

class _BookingsPageState extends State<BookingsPage>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  late BookingApiService _bookingService;
  List<BookingModel> _bookings = [];
  bool _isLoading = true;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 3, vsync: this);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final authService = AuthScope.of(context);
    _bookingService = BookingApiService(
      ApiClient(storage: authService.storage),
    );
    _loadBookings();
  }

  Future<void> _loadBookings() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });
    try {
      final items = await _bookingService.fetchMyBookings();
      if (mounted) setState(() => _bookings = items);
    } catch (e) {
      if (mounted) setState(() => _errorMessage = e.toString());
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  List<BookingModel> _filterByTab(int tabIndex) {
    return filterBookingsByTab(_bookings, tabIndex);
  }

  Future<void> _confirmDeleteDraft(BookingModel booking) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete Draft Booking?'),
        content: const Text('Are you sure you want to delete this draft booking? This action cannot be undone.'),
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
    ) ?? false;

    if (!confirmed || !mounted) return;

    try {
      await _bookingService.deleteBooking(booking.id);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Draft booking deleted.')));
      await _loadBookings();
    } catch (err) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(err.toString())));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('My Bookings'),
        bottom: TabBar(
          controller: _tabController,
          tabs: const [
            Tab(text: 'Active'),
            Tab(text: 'Confirmed'),
            Tab(text: 'History'),
          ],
        ),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _errorMessage != null
              ? Center(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(_errorMessage!, style: const TextStyle(color: Colors.red)),
                      const SizedBox(height: 12),
                      FilledButton(
                        onPressed: _loadBookings,
                        child: const Text('Retry'),
                      ),
                    ],
                  ),
                )
              : TabBarView(
                  controller: _tabController,
                  children: [
                    _buildList(_filterByTab(0)),
                    _buildList(_filterByTab(1)),
                    _buildList(_filterByTab(2)),
                  ],
                ),
    );
  }

  Widget _buildList(List<BookingModel> list) {
    if (list.isEmpty) {
      return RefreshIndicator(
        onRefresh: _loadBookings,
        child: ListView(
          children: const [
            SizedBox(height: 100),
            Center(child: Text('No bookings in this category.')),
          ],
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _loadBookings,
      child: ListView.builder(
        padding: const EdgeInsets.all(16),
        itemCount: list.length,
        itemBuilder: (ctx, idx) {
          final item = list[idx];
          final now = DateTime.now();
          final activeAdvisories = item.activeAdvisories
              .where((a) => a.endDateTime == null || a.endDateTime!.isAfter(now))
              .toList();

          return Card(
            margin: const EdgeInsets.only(bottom: 12),
            child: InkWell(
              borderRadius: BorderRadius.circular(12),
              onTap: () {
                Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (c) => BookingDetailPage(
                      booking: item,
                      bookingService: _bookingService,
                      onBookingUpdated: _loadBookings,
                    ),
                  ),
                );
              },
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          'ID: ${item.id.substring(0, 8)}...',
                          style: const TextStyle(
                            fontFamily: 'monospace',
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Chip(
                              label: Text(
                                item.status,
                                style: const TextStyle(fontSize: 12, fontWeight: FontWeight.bold),
                              ),
                            ),
                            if (item.status == 'Draft') ...[
                              const SizedBox(width: 4),
                              IconButton(
                                icon: const Icon(Icons.delete_outline, size: 20, color: Colors.red),
                                tooltip: 'Delete draft booking',
                                onPressed: () => _confirmDeleteDraft(item),
                              ),
                            ],
                          ],
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Text(
                      '${item.items.length} item(s) reserved',
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Total: LKR ${item.totalAmount.toStringAsFixed(2)}',
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        color: Theme.of(context).colorScheme.primary,
                      ),
                    ),
                    if (activeAdvisories.isNotEmpty) ...[
                      const SizedBox(height: 10),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
                        decoration: BoxDecoration(
                          color: Colors.amber.shade50,
                          borderRadius: BorderRadius.circular(8),
                          border: Border.all(color: Colors.amber.shade400),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                const Icon(Icons.warning_amber_rounded, size: 18, color: Colors.orange),
                                const SizedBox(width: 6),
                                Expanded(
                                  child: Text(
                                    'Advisory in ${activeAdvisories.first.district}: ${activeAdvisories.first.title}',
                                    style: const TextStyle(
                                      fontSize: 12,
                                      fontWeight: FontWeight.w600,
                                      color: Colors.black87,
                                    ),
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 3),
                            Text(
                              _formatAlertTime(activeAdvisories.first.startDateTime, activeAdvisories.first.endDateTime),
                              style: TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w600,
                                color: Colors.orange.shade900,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ),
          );
        },
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
