import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';

import '../models/agent_workflow_model.dart';
import '../models/trip_model.dart';
import '../services/api_client.dart';
import '../services/travel_intelligence_api_service.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';
import '../utils/attraction_images.dart';
import '../widgets/auth_scope.dart';
import '../widgets/itinerary_map.dart';
import 'trip_widgets.dart';

class ItineraryPage extends StatefulWidget {
  const ItineraryPage({
    required this.api,
    required this.tripId,
    this.workflowSource,
    super.key,
  });
  final TripApiService api;
  final String tripId;
  final AgentWorkflowSource? workflowSource;
  @override
  State<ItineraryPage> createState() => _ItineraryPageState();
}

class _ItineraryPageState extends State<ItineraryPage> {
  Itinerary? _itinerary;
  Trip? _trip;
  List<ItineraryHistoryItem> _history = const [];
  AgentWorkflow? _workflow;
  AgentWorkflowSource? _workflowSource;
  String? _error;
  var _loading = true;
  var _loaded = false;
  final _daysKey = GlobalKey();

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _workflowSource ??=
        widget.workflowSource ??
        TravelIntelligenceApiService(
          ApiClient(storage: AuthScope.of(context).storage),
        );
    if (!_loaded) {
      _loaded = true;
      _load();
    }
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        widget.api.getItinerary(widget.tripId),
        widget.api.getItineraryHistory(widget.tripId),
      ]);
      Trip? trip;
      try {
        trip = await widget.api.getTrip(widget.tripId);
      } on ApiException {}
      AgentWorkflow? workflow;
      try {
        workflow = await _workflowSource!.fetchWorkflow(widget.tripId);
      } on ApiException catch (error) {
        if (error.statusCode != 404) rethrow;
      }
      if (mounted)
        setState(() {
          _trip = trip;
          _itinerary = results[0] as Itinerary?;
          _history = results[1] as List<ItineraryHistoryItem>;
          _workflow = workflow;
          _loading = false;
        });
    } catch (error) {
      if (mounted)
        setState(() {
          _error = error.toString();
          _loading = false;
        });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading)
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    if (_error != null)
      return Scaffold(
        appBar: AppBar(title: const Text('Your itinerary')),
        body: ErrorState(message: _error!, onRetry: _load),
      );
    final itinerary = _itinerary;
    return Scaffold(
      appBar: AppBar(title: const Text('Your journey')),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 900),
          child: itinerary == null
              ? const Padding(
                  padding: EdgeInsets.all(CeylonSpacing.lg),
                  child: Center(
                    child: Text('Your itinerary is not ready yet.'),
                  ),
                )
            : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(
                      CeylonSpacing.md,
                      CeylonSpacing.sm,
                      CeylonSpacing.md,
                      48,
                    ),
                    children: [
                      _TripHero(
                        trip: _trip,
                        itinerary: _withTripDays(itinerary, _trip),
                      ),
                      const SizedBox(height: CeylonSpacing.md),
                      _BudgetSummary(
                        trip: _trip,
                        itinerary: _withTripDays(itinerary, _trip),
                      ),
                      const SizedBox(height: CeylonSpacing.md),
                      _WorkspaceActions(
                        itinerary: itinerary,
                        onItinerary: () => Scrollable.ensureVisible(
                          _daysKey.currentContext!,
                          duration: const Duration(milliseconds: 350),
                        ),
                      ),
                      const SizedBox(height: CeylonSpacing.lg),
                      _TravelSummary(
                        trip: _trip,
                        itinerary: _withTripDays(itinerary, _trip),
                      ),
                      const SizedBox(height: CeylonSpacing.lg),
                      _WorkflowSummary(workflow: _workflow),
                      const SizedBox(height: CeylonSpacing.xl),
                      _DaysSection(
                        key: _daysKey,
                        tripId: widget.tripId,
                        itinerary: _withTripDays(itinerary, _trip),
                      ),
                      if (_history.length > 1) ...[
                        const SizedBox(height: CeylonSpacing.xl),
                        Text(
                          'Previous versions',
                          style: Theme.of(context).textTheme.titleLarge,
                        ),
                        const SizedBox(height: CeylonSpacing.sm),
                        ..._history
                            .skip(1)
                            .map(
                              (version) => Card(
                                child: ListTile(
                                  title: Text(
                                    version.createdAt == null
                                        ? 'Earlier itinerary'
                                        : displayDate(version.createdAt!),
                                  ),
                                  subtitle: Text(
                                    '${friendlyStatus(version.status)}  •  ${displayMoney(version.totalEstimatedCost)}',
                                  ),
                                  onTap: () async {
                                    final selected = await widget.api
                                        .getItineraryVersion(
                                          widget.tripId,
                                          version.id,
                                        );
                                    if (mounted)
                                      setState(() => _itinerary = selected);
                                  },
                                ),
                              ),
                            ),
                      ],
                    ],
                  ),
                ),
        ),
      ),
    );
  }
}

Itinerary _withTripDays(Itinerary itinerary, Trip? trip) {
  if (trip == null) return itinerary;

  final dayCount = trip.endDate.difference(trip.startDate).inDays + 1;
  if (dayCount <= 0) return itinerary;

  final existing = {
    for (final day in itinerary.days) day.dayNumber: day,
  };
  final days = List<ItineraryDay>.generate(
    dayCount,
    (index) => existing[index + 1] ??
        ItineraryDay(
          id: '',
          dayNumber: index + 1,
          date: trip.startDate.add(Duration(days: index)),
          items: const [],
        ),
  );

  return Itinerary(
    id: itinerary.id,
    tripId: itinerary.tripId,
    status: itinerary.status,
    totalEstimatedCost: itinerary.totalEstimatedCost,
    createdAt: itinerary.createdAt,
    updatedAt: itinerary.updatedAt,
    days: days,
  );
}

class _TripHero extends StatelessWidget {
  const _TripHero({required this.trip, required this.itinerary});
  final Trip? trip;
  final Itinerary itinerary;
  @override
  Widget build(BuildContext context) {
    final allItems = itinerary.days.expand((day) => day.items).toList();
    final first = allItems.isEmpty ? null : allItems.first;
    final title = trip != null && trip!.name.trim().isNotEmpty
        ? trip!.name
        : 'Your Sri Lanka journey';
    final destination = first?.attractionName?.trim().isNotEmpty == true
        ? first!.attractionName!
        : (trip?.preferences.isNotEmpty == true
              ? trip!.preferences.first.value
              : 'Sri Lanka');
    final asset = destinationHeroAsset('$title $destination');
    return ClipRRect(
      borderRadius: BorderRadius.circular(CeylonRadii.card),
      child: Stack(
        children: [
          SizedBox(
            height: 250,
            width: double.infinity,
            child: asset == null
                ? Container(color: CeylonColors.forest)
                : Image.asset(
                    asset,
                    fit: BoxFit.cover,
                    filterQuality: FilterQuality.high,
                    errorBuilder: (_, __, ___) =>
                        Container(color: CeylonColors.forest),
                  ),
          ),
          Positioned.fill(
            child: DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  colors: [
                    Colors.transparent,
                    Colors.black.withValues(alpha: .78),
                  ],
                ),
              ),
            ),
          ),
          Positioned(
            left: CeylonSpacing.md,
            right: CeylonSpacing.md,
            bottom: CeylonSpacing.md,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  destination.toUpperCase(),
                  style: const TextStyle(
                    color: CeylonColors.mint,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 1.1,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  title,
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                    color: Colors.white,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                if (trip != null)
                  Text(
                    '${displayDate(trip!.startDate)} – ${displayDate(trip!.endDate)}  •  ${tripDuration(trip!)}',
                    style: const TextStyle(color: Colors.white70),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _BudgetSummary extends StatelessWidget {
  const _BudgetSummary({required this.trip, required this.itinerary});
  final Trip? trip;
  final Itinerary itinerary;
  @override
  Widget build(BuildContext context) {
    final budget = trip?.budget;
    final remaining = budget == null
        ? null
        : budget - itinerary.totalEstimatedCost;
    return Wrap(
      spacing: CeylonSpacing.sm,
      runSpacing: CeylonSpacing.sm,
      children: [
        _Metric(
          label: 'Trip budget',
          value: budget == null ? 'Not available' : displayMoney(budget),
        ),
        _Metric(
          label: 'Planned cost',
          value: displayMoney(itinerary.totalEstimatedCost),
        ),
        _Metric(
          label: 'Remaining',
          value: remaining == null ? 'Not available' : displayMoney(remaining),
          accent: remaining != null && remaining < 0
              ? CeylonColors.error
              : CeylonColors.teal,
        ),
      ],
    );
  }
}

class _Metric extends StatelessWidget {
  const _Metric({required this.label, required this.value, this.accent});
  final String label, value;
  final Color? accent;
  @override
  Widget build(BuildContext context) => Container(
    width: 270,
    padding: const EdgeInsets.all(CeylonSpacing.md),
    decoration: BoxDecoration(
      color: CeylonColors.surfaceSoft,
      borderRadius: BorderRadius.circular(CeylonRadii.card),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(color: CeylonColors.inkMuted)),
        const SizedBox(height: 4),
        Text(
          value,
          style: TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w800,
            color: accent ?? CeylonColors.ink,
          ),
        ),
      ],
    ),
  );
}

class _WorkspaceActions extends StatelessWidget {
  const _WorkspaceActions({required this.itinerary, required this.onItinerary});
  final Itinerary itinerary;
  final VoidCallback onItinerary;
  @override
  Widget build(BuildContext context) => Row(
    children: [
      Expanded(
        child: FilledButton.icon(
          onPressed: onItinerary,
          icon: const Icon(Icons.view_timeline_outlined),
          label: const Text('Itinerary'),
        ),
      ),
      const SizedBox(width: CeylonSpacing.sm),
      Expanded(
        child: OutlinedButton.icon(
          onPressed: () => _showMapList(context),
          icon: const Icon(Icons.map_outlined),
          label: const Text('Map'),
        ),
      ),
    ],
  );
  void _showMapList(BuildContext context) {
    final items = itinerary.days
        .expand((day) => day.items)
        .where((item) => item.latitude != null && item.longitude != null)
        .toList();
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (_) => SafeArea(
        child: SizedBox(
          height: items.isEmpty ? 180 : 560,
          child: Column(
            children: [
              if (items.isNotEmpty)
                Expanded(child: ItineraryMapPanel(items: items))
              else
                const Expanded(child: ItineraryMapPanel(items: const [])),
              if (items.isNotEmpty)
                SizedBox(
                  height: 72,
                  child: ListView(
                    scrollDirection: Axis.horizontal,
                    padding: const EdgeInsets.symmetric(
                      horizontal: CeylonSpacing.md,
                    ),
                    children: items
                        .map(
                          (item) => Padding(
                            padding: const EdgeInsets.only(right: 8),
                            child: OutlinedButton.icon(
                              onPressed: () => _openMap(context, item),
                              icon: const Icon(Icons.open_in_new, size: 16),
                              label: Text(
                                item.attractionName ?? 'Open location',
                              ),
                            ),
                          ),
                        )
                        .toList(),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _openMap(BuildContext context, ItineraryItem item) async {
    final uri = Uri.parse(
      'https://www.google.com/maps/search/?api=1&query=${item.latitude},${item.longitude}',
    );
    if (await launchUrl(uri, mode: LaunchMode.externalApplication) &&
        context.mounted)
      Navigator.pop(context);
  }
}

class _TravelSummary extends StatelessWidget {
  const _TravelSummary({required this.trip, required this.itinerary});
  final Trip? trip;
  final Itinerary itinerary;
  @override
  Widget build(BuildContext context) {
    final places = itinerary.days.fold<int>(
      0,
      (sum, day) => sum + day.items.length,
    );
    return SectionCard(
      child: Wrap(
        spacing: CeylonSpacing.lg,
        runSpacing: CeylonSpacing.sm,
        children: [
          _Fact(
            icon: Icons.place_outlined,
            label: 'Places',
            value: '$places planned',
          ),
          _Fact(
            icon: Icons.verified_outlined,
            label: 'AI checks',
            value: 'Reviewed',
          ),
          _Fact(
            icon: Icons.account_balance_wallet_outlined,
            label: 'Budget',
            value: trip?.budget == null ? 'Not available' : 'Tracked',
          ),
        ],
      ),
    );
  }
}

class _Fact extends StatelessWidget {
  const _Fact({required this.icon, required this.label, required this.value});
  final IconData icon;
  final String label, value;
  @override
  Widget build(BuildContext context) => SizedBox(
    width: 150,
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, color: CeylonColors.teal),
        const SizedBox(width: 8),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label, style: const TextStyle(color: CeylonColors.inkMuted)),
              Text(value, style: const TextStyle(fontWeight: FontWeight.w800)),
            ],
          ),
        ),
      ],
    ),
  );
}

class _DaysSection extends StatelessWidget {
  const _DaysSection({
    required this.tripId,
    required this.itinerary,
    super.key,
  });
  final String tripId;
  final Itinerary itinerary;
  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text('Your journey', style: Theme.of(context).textTheme.titleLarge),
      const SizedBox(height: CeylonSpacing.sm),
      if (itinerary.days.isNotEmpty)
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: itinerary.days
                .map(
                  (day) => Padding(
                    padding: const EdgeInsets.only(right: CeylonSpacing.sm),
                    child: ActionChip(
                      avatar: CircleAvatar(
                        backgroundColor: CeylonColors.forest,
                        child: Text(
                          '${day.dayNumber}',
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 12,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      label: Text('Day ${day.dayNumber}'),
                      onPressed: () => context.push(
                        '/trips/$tripId/itinerary/day/${day.dayNumber}',
                        extra: day,
                      ),
                    ),
                  ),
                )
                .toList(),
          ),
        ),
      const SizedBox(height: CeylonSpacing.sm),
      ...itinerary.days.map((day) => _DayCard(tripId: tripId, day: day)),
    ],
  );
}

class _DayCard extends StatelessWidget {
  const _DayCard({required this.tripId, required this.day});
  final String tripId;
  final ItineraryDay day;
  @override
  Widget build(BuildContext context) {
    final preview = day.items.take(3).toList();
    return Padding(
      padding: const EdgeInsets.only(bottom: CeylonSpacing.sm),
      child: Card(
        child: InkWell(
          borderRadius: BorderRadius.circular(CeylonRadii.card),
          onTap: () => context.push(
            '/trips/$tripId/itinerary/day/${day.dayNumber}',
            extra: day,
          ),
          child: Padding(
            padding: const EdgeInsets.all(CeylonSpacing.md),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Container(
                      width: 46,
                      height: 46,
                      decoration: BoxDecoration(
                        color: CeylonColors.mint,
                        borderRadius: BorderRadius.circular(14),
                      ),
                      child: Center(
                        child: Text(
                          '${day.dayNumber}',
                          style: const TextStyle(
                            fontSize: 20,
                            fontWeight: FontWeight.w800,
                            color: CeylonColors.forest,
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: CeylonSpacing.md),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Day ${day.dayNumber}',
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                          Text(
                            '${displayDate(day.date)}  •  ${day.items.length} ${day.items.length == 1 ? 'place' : 'places'}',
                            style: const TextStyle(
                              color: CeylonColors.inkMuted,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const Icon(
                      Icons.chevron_right_rounded,
                      color: CeylonColors.inkMuted,
                    ),
                  ],
                ),
                if (preview.isNotEmpty) ...[
                  const Divider(height: CeylonSpacing.lg),
                  ...preview.map(
                    (item) => Padding(
                      padding: const EdgeInsets.only(bottom: 6),
                      child: Row(
                        children: [
                          const Icon(
                            Icons.schedule,
                            size: 16,
                            color: CeylonColors.tea,
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              '${item.startTime}  ${item.attractionName ?? 'Planned experience'}',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
                if (day.items.length > preview.length)
                  Text(
                    '+ ${day.items.length - preview.length} more planned place(s)',
                    style: const TextStyle(
                      color: CeylonColors.teal,
                      fontWeight: FontWeight.w700,
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

class _WorkflowSummary extends StatelessWidget {
  const _WorkflowSummary({required this.workflow});
  final AgentWorkflow? workflow;
  @override
  Widget build(BuildContext context) {
    final value = workflow;
    if (value == null)
      return const SectionCard(
        child: Text('AI planning checks are being prepared.'),
      );
    final stages = [...value.stages]
      ..sort((a, b) => a.sequence.compareTo(b.sequence));
    return SectionCard(
      child: ExpansionTile(
        tilePadding: EdgeInsets.zero,
        childrenPadding: EdgeInsets.zero,
        title: const Text('AI planning checks'),
        subtitle: Text(
          value.reviewStatus == null
              ? 'Grounding and travel checks recorded'
              : _reviewLabel(value),
        ),
        children: [
          ...stages.map(
            (stage) => _ProgressLine(
              icon: _stageDone(stage)
                  ? Icons.check_circle_outline
                  : Icons.radio_button_unchecked,
              label: _stageLabel(stage.agentRole),
              done: _stageDone(stage),
            ),
          ),
          if (value.executionSucceeded == true)
            const _ProgressLine(
              icon: Icons.check_circle_outline,
              label: 'Booking confirmed',
            ),
        ],
      ),
    );
  }
}

class _ProgressLine extends StatelessWidget {
  const _ProgressLine({
    required this.icon,
    required this.label,
    this.done = true,
  });
  final IconData icon;
  final String label;
  final bool done;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: CeylonSpacing.sm),
    child: Row(
      children: [
        Icon(
          icon,
          size: 20,
          color: done ? CeylonColors.teal : CeylonColors.inkMuted,
        ),
        const SizedBox(width: CeylonSpacing.sm),
        Expanded(
          child: Text(
            label,
            style: TextStyle(
              fontWeight: done ? FontWeight.w700 : FontWeight.w500,
              color: done ? CeylonColors.ink : CeylonColors.inkMuted,
            ),
          ),
        ),
      ],
    ),
  );
}

bool _stageDone(AgentWorkflowStage stage) {
  final value = stage.status.toLowerCase();
  return value.contains('complete') ||
      value.contains('success') ||
      value.contains('approved');
}

String _stageLabel(String role) {
  switch (role) {
    case 'Planner':
      return 'Plan created';
    case 'Destination':
      return 'Places selected';
    case 'BookingAction':
      return 'Experiences checked';
    case 'TravelIntelligence':
      return 'Travel and safety checks complete';
    default:
      return 'Journey details checked';
  }
}

String _reviewLabel(AgentWorkflow workflow) {
  final status = workflow.reviewStatus?.toLowerCase();
  if (status == 'approved') return 'Approved';
  if (status == 'pending' || status == 'awaitingapproval')
    return 'Waiting for review';
  return friendlyStatus(workflow.reviewStatus ?? '');
}
