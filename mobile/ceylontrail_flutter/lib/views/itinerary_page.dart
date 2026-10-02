import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/agent_workflow_model.dart';
import '../models/trip_model.dart';
import '../services/api_client.dart';
import '../services/travel_intelligence_api_service.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/auth_scope.dart';
import 'trip_widgets.dart';

class ItineraryPage extends StatefulWidget {
  const ItineraryPage({required this.api, required this.tripId, this.workflowSource, super.key});
  final TripApiService api;
  final String tripId;
  final AgentWorkflowSource? workflowSource;
  @override State<ItineraryPage> createState() => _ItineraryPageState();
}

class _ItineraryPageState extends State<ItineraryPage> {
  Itinerary? _itinerary;
  List<ItineraryHistoryItem> _history = const [];
  AgentWorkflow? _workflow;
  AgentWorkflowSource? _workflowSource;
  String? _error;
  var _loading = true;
  var _loaded = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _workflowSource ??= widget.workflowSource ??
        TravelIntelligenceApiService(
          ApiClient(storage: AuthScope.of(context).storage),
        );
    if (!_loaded) {
      _loaded = true;
      _load();
    }
  }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final results = await Future.wait([
        widget.api.getItinerary(widget.tripId),
        widget.api.getItineraryHistory(widget.tripId),
      ]);
      AgentWorkflow? workflow;
      try {
        workflow = await _workflowSource!.fetchWorkflow(widget.tripId);
      } on ApiException catch (error) {
        if (error.statusCode != 404) rethrow;
      }
      if (mounted) {
        setState(() {
          _itinerary = results[0] as Itinerary?;
          _history = results[1] as List<ItineraryHistoryItem>;
          _workflow = workflow;
          _loading = false;
        });
      }
    } catch (error) {
      if (mounted) setState(() { _error = error.toString(); _loading = false; });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (_error != null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Your itinerary')),
        body: ErrorState(message: _error!, onRetry: _load),
      );
    }

    final itinerary = _itinerary;
    return Scaffold(
      appBar: AppBar(title: const Text('Your itinerary')),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 760),
          child: itinerary == null
              ? const Padding(
                  padding: EdgeInsets.all(CeylonSpacing.lg),
                  child: Center(
                    child: Text(
                      'Your itinerary is not ready yet.',
                      textAlign: TextAlign.center,
                    ),
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
                      _ItineraryHeader(itinerary: itinerary),
                      const SizedBox(height: CeylonSpacing.lg),
                      _WorkflowSummary(workflow: _workflow),
                      const SizedBox(height: CeylonSpacing.xl),
                      Text(
                        'Your days',
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
                      const SizedBox(height: CeylonSpacing.sm),
                      ...itinerary.days.map(
                        (day) => _DayCard(tripId: widget.tripId, day: day),
                      ),
                      if (_history.length > 1) ...[
                        const SizedBox(height: CeylonSpacing.xl),
                        Text(
                          'Previous versions',
                          style: Theme.of(context).textTheme.titleLarge,
                        ),
                        const SizedBox(height: CeylonSpacing.sm),
                        ..._history.skip(1).map(
                          (version) => Card(
                            child: ListTile(
                              title: Text(
                                version.createdAt == null
                                    ? 'Earlier itinerary'
                                    : displayDate(version.createdAt!),
                              ),
                              subtitle: Text(
                                friendlyStatus(version.status) +
                                    '  \u2022  ' +
                                    displayMoney(version.totalEstimatedCost),
                              ),
                              onTap: () async {
                                final selected =
                                    await widget.api.getItineraryVersion(
                                  widget.tripId,
                                  version.id,
                                );
                                if (mounted) {
                                  setState(() => _itinerary = selected);
                                }
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

class _ItineraryHeader extends StatelessWidget {
  const _ItineraryHeader({required this.itinerary});
  final Itinerary itinerary;

  @override
  Widget build(BuildContext context) {
    return SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Your itinerary',
                style: Theme.of(context).textTheme.headlineMedium,
              ),
              TripStatusChip(status: itinerary.status),
            ],
          ),
          const SizedBox(height: CeylonSpacing.sm),
          Text(
            itinerary.days.length.toString() +
                (itinerary.days.length == 1 ? ' day planned  ' : ' days planned  ') +
                '\u2022  ' +
                displayMoney(itinerary.totalEstimatedCost),
          ),
          if (itinerary.createdAt != null)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                'Created ' + displayDate(itinerary.createdAt!),
                style: const TextStyle(color: CeylonColors.inkMuted),
              ),
            ),
        ],
      ),
    );
  }
}

class _DayCard extends StatelessWidget {
  const _DayCard({required this.tripId, required this.day});
  final String tripId;
  final ItineraryDay day;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: InkWell(
        onTap: () => context.push(
          '/trips/' + tripId + '/itinerary/day/' + day.dayNumber.toString(),
          extra: day,
        ),
        borderRadius: BorderRadius.circular(CeylonRadii.card),
        child: Padding(
          padding: const EdgeInsets.all(CeylonSpacing.md),
          child: Row(
            children: [
              Container(
                width: 52,
                height: 52,
                decoration: BoxDecoration(
                  color: CeylonColors.mint,
                  borderRadius: BorderRadius.circular(16),
                ),
                child: Center(
                  child: Text(
                    day.dayNumber.toString(),
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
                      'Day ' + day.dayNumber.toString(),
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 3),
                    Text(displayDate(day.date)),
                    Text(
                      day.items.length.toString() +
                          (day.items.length == 1
                              ? ' experience'
                              : ' experiences'),
                      style: const TextStyle(color: CeylonColors.inkMuted),
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
    if (value == null) {
      return const SectionCard(child: Text('Your journey is being prepared.'));
    }
    final stages = [...value.stages]
      ..sort((a, b) => a.sequence.compareTo(b.sequence));
    return SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Journey progress', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: CeylonSpacing.md),
          if (value.reviewStatus != null)
            _ProgressLine(
              icon: Icons.rate_review_outlined,
              label: _reviewLabel(value),
            ),
          if (value.executionSucceeded == true)
            const _ProgressLine(
              icon: Icons.check_circle_outline,
              label: 'Booking confirmed',
            ),
          ...stages.map(
            (stage) => _ProgressLine(
              icon: _stageDone(stage)
                  ? Icons.check_circle_outline
                  : Icons.radio_button_unchecked,
              label: _stageLabel(stage.agentRole),
              done: _stageDone(stage),
            ),
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
  Widget build(BuildContext context) {
    return Padding(
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
  if (status == 'pending' || status == 'awaitingapproval') {
    return 'Waiting for review';
  }
  return friendlyStatus(workflow.reviewStatus ?? '');
}
