import 'package:flutter/material.dart';

import '../config/api_config.dart';
import '../models/booking_model.dart';
import '../services/api_client.dart';
import '../services/booking_api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/auth_scope.dart';
import 'booking_detail_page.dart';

List<BookingModel> filterBookingsByTab(List<BookingModel> bookings, int tabIndex) {
  if (tabIndex == 0) {
    return bookings.where((booking) => const {
      'Draft', 'PendingAI', 'PendingHumanApproval', 'Confirmed',
    }.contains(booking.status)).toList();
  }
  if (tabIndex == 1) {
    return bookings.where((booking) => booking.status == 'Confirmed').toList();
  }
  return bookings.where((booking) => const {
    'Cancelled', 'Rejected', 'Completed',
  }.contains(booking.status)).toList();
}

class BookingsPage extends StatefulWidget {
  const BookingsPage({super.key});

  @override
  State<BookingsPage> createState() => _BookingsPageState();
}

class _BookingsPageState extends State<BookingsPage>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;
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
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _bookingService = BookingApiService(
      ApiClient(storage: AuthScope.of(context).storage),
    );
    _loadBookings();
  }

  Future<void> _loadBookings() async {
    if (mounted) {
      setState(() {
        _isLoading = true;
        _errorMessage = null;
      });
    }
    try {
      final items = await _bookingService.fetchMyBookings();
      if (mounted) setState(() => _bookings = items);
    } catch (error) {
      if (mounted) setState(() => _errorMessage = error.toString());
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _confirmDeleteDraft(BookingModel booking) async {
    final confirmed = await showDialog<bool>(
          context: context,
          builder: (dialogContext) => AlertDialog(
            title: const Text('Delete draft booking?'),
            content: const Text('Are you sure you want to remove this draft booking?'),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(false),
                child: const Text('Keep it'),
              ),
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
    try {
      await _bookingService.deleteBooking(booking.id);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Draft booking deleted.')),
      );
      await _loadBookings();
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(error.toString())),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            _buildHeader(),
            _buildTabs(),
            Expanded(
              child: _isLoading
                  ? const Center(child: CircularProgressIndicator())
                  : _errorMessage != null
                      ? _buildError()
                      : TabBarView(
                          controller: _tabController,
                          children: [
                            _buildList(filterBookingsByTab(_bookings, 0)),
                            _buildList(filterBookingsByTab(_bookings, 1)),
                            _buildList(filterBookingsByTab(_bookings, 2)),
                          ],
                        ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildHeader() {
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 920),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 24, 20, 18),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('My bookings', style: Theme.of(context).textTheme.displaySmall),
                    const SizedBox(height: 6),
                    Text("Everything you've reserved for your journeys.",
                        style: Theme.of(context).textTheme.bodyMedium),
                  ],
                ),
              ),
              IconButton(
                tooltip: 'Refresh bookings',
                onPressed: _isLoading ? null : _loadBookings,
                icon: const Icon(Icons.refresh_rounded),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildTabs() {
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 920),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 20),
          child: TabBar(
            controller: _tabController,
            isScrollable: true,
            tabAlignment: TabAlignment.start,
            labelColor: CeylonColors.forest,
            unselectedLabelColor: CeylonColors.inkMuted,
            indicatorColor: CeylonColors.forest,
            tabs: const [
              Tab(text: 'Active'),
              Tab(text: 'Confirmed'),
              Tab(text: 'History'),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildError() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.cloud_off_rounded, size: 42),
            const SizedBox(height: 12),
            const Text('We could not load your bookings.'),
            const SizedBox(height: 12),
            FilledButton(onPressed: _loadBookings, child: const Text('Retry')),
          ],
        ),
      ),
    );
  }

  Widget _buildList(List<BookingModel> bookings) {
    if (bookings.isEmpty) {
      return RefreshIndicator(
        onRefresh: _loadBookings,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: const [
            SizedBox(height: 100),
            Icon(Icons.explore_outlined, size: 42, color: CeylonColors.tea),
            SizedBox(height: 14),
            Center(child: Text('No bookings in this category.')),
          ],
        ),
      );
    }
    return RefreshIndicator(
      onRefresh: _loadBookings,
      child: ListView.builder(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
        itemCount: bookings.length,
        itemBuilder: (_, index) => _BookingCard(
          booking: bookings[index],
          onTap: () => Navigator.of(context).push(MaterialPageRoute(
            builder: (_) => BookingDetailPage(
              booking: bookings[index],
              bookingService: _bookingService,
              onBookingUpdated: _loadBookings,
            ),
          )),
          onDeleteDraft: bookings[index].status == 'Draft'
              ? () => _confirmDeleteDraft(bookings[index])
              : null,
        ),
      ),
    );
  }
}

class _BookingCard extends StatelessWidget {
  const _BookingCard({required this.booking, required this.onTap, this.onDeleteDraft});

  final BookingModel booking;
  final VoidCallback onTap;
  final VoidCallback? onDeleteDraft;

  @override
  Widget build(BuildContext context) {
    final status = bookingStatusPresentation(booking.status);
    final firstItem = booking.items.isNotEmpty ? booking.items.first : null;
    final primaryImageUrl = booking.items
        .where((i) => i.attractionImageUrl != null && i.attractionImageUrl!.isNotEmpty)
        .firstOrNull
        ?.attractionImageUrl;

    return Card(
      margin: const EdgeInsets.only(bottom: 16),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _ReservationVisual(imageUrl: primaryImageUrl),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(booking.displayTitle,
                            style: Theme.of(context).textTheme.titleMedium),
                        const SizedBox(height: 8),
                        _StatusChip(status: status),
                      ],
                    ),
                  ),
                  if (onDeleteDraft != null)
                    IconButton(
                      tooltip: 'Delete draft booking',
                      onPressed: onDeleteDraft,
                      icon: const Icon(Icons.delete_outline),
                    ),
                ],
              ),
              const SizedBox(height: 16),
              Wrap(
                spacing: 18,
                runSpacing: 8,
                children: [
                  if (firstItem?.district != null && firstItem!.district!.isNotEmpty)
                    _InfoItem(
                      icon: Icons.location_on_outlined,
                      text: firstItem.district!,
                    ),
                  if (guestCount > 0)
                    _InfoItem(
                      icon: Icons.people_outline,
                      text: '$guestCount ${guestCount == 1 ? 'guest' : 'guests'}',
                    ),
                  _InfoItem(icon: Icons.payments_outlined, text: formatLkr(booking.totalAmount)),
                ],
              ),
              const SizedBox(height: 16),
              const Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  Text('View booking', style: TextStyle(color: CeylonColors.forest, fontWeight: FontWeight.w800)),
                  SizedBox(width: 6),
                  Icon(Icons.arrow_forward_rounded, size: 18, color: CeylonColors.forest),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ReservationVisual extends StatelessWidget {
  const _ReservationVisual({this.imageUrl});

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
                width: 58,
                height: 58,
                fit: BoxFit.cover,
                errorBuilder: (context, error, stackTrace) => Container(
                  width: 58,
                  height: 58,
                  decoration: BoxDecoration(color: CeylonColors.mint, borderRadius: BorderRadius.circular(16)),
                  child: const Icon(Icons.confirmation_number_outlined, color: CeylonColors.forest, size: 28),
                ),
              )
            : Image.asset(
                resolvedUrl,
                width: 58,
                height: 58,
                fit: BoxFit.cover,
                errorBuilder: (context, error, stackTrace) => Container(
                  width: 58,
                  height: 58,
                  decoration: BoxDecoration(color: CeylonColors.mint, borderRadius: BorderRadius.circular(16)),
                  child: const Icon(Icons.confirmation_number_outlined, color: CeylonColors.forest, size: 28),
                ),
              ),
      );
    }
    return Container(
      width: 58,
      height: 58,
      decoration: BoxDecoration(color: CeylonColors.mint, borderRadius: BorderRadius.circular(16)),
      child: const Icon(Icons.confirmation_number_outlined, color: CeylonColors.forest, size: 28),
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

class _InfoItem extends StatelessWidget {
  const _InfoItem({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 18, color: CeylonColors.inkMuted),
        const SizedBox(width: 6),
        Text(text, style: Theme.of(context).textTheme.bodyMedium),
      ],
    );
  }
}

class BookingStatusPresentation {
  const BookingStatusPresentation({required this.label, required this.icon, required this.foreground, required this.background});

  final String label;
  final IconData icon;
  final Color foreground;
  final Color background;
}

BookingStatusPresentation bookingStatusPresentation(String status) {
  switch (status) {
    case 'Confirmed':
      return const BookingStatusPresentation(label: 'Confirmed', icon: Icons.check_circle_outline, foreground: CeylonColors.tea, background: CeylonColors.mint);
    case 'PendingAI':
    case 'PendingHumanApproval':
      return const BookingStatusPresentation(label: 'Waiting for confirmation', icon: Icons.schedule_outlined, foreground: Color(0xFF8A5A18), background: Color(0xFFFFF1D6));
    case 'Cancelled':
      return const BookingStatusPresentation(label: 'Cancelled', icon: Icons.cancel_outlined, foreground: CeylonColors.error, background: Color(0xFFFBE8E8));
    case 'Rejected':
      return const BookingStatusPresentation(label: 'Not confirmed', icon: Icons.info_outline, foreground: CeylonColors.error, background: Color(0xFFFBE8E8));
    case 'Completed':
      return const BookingStatusPresentation(label: 'Completed', icon: Icons.check_circle_outline, foreground: CeylonColors.inkMuted, background: CeylonColors.surfaceSoft);
    case 'Draft':
    default:
      return const BookingStatusPresentation(label: 'Draft', icon: Icons.edit_note_outlined, foreground: CeylonColors.inkMuted, background: CeylonColors.surfaceSoft);
  }
}

String formatLkr(double amount) {
  final rounded = amount.round().toString();
  final formatted = rounded.replaceAllMapped(RegExp(r'\B(?=(\d{3})+(?!\d))'), (_) => ',');
  return 'LKR $formatted';
}
