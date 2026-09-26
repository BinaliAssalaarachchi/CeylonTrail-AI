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
                        Chip(
                          label: Text(
                            item.status,
                            style: const TextStyle(fontSize: 12, fontWeight: FontWeight.bold),
                          ),
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
                      'Total: \$${item.totalAmount.toStringAsFixed(2)}',
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        color: Theme.of(context).colorScheme.primary,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}
