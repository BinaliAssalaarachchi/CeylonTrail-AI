import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/trip_model.dart';
import '../models/agent_workflow_model.dart';
import '../services/api_client.dart';
import '../services/trip_api_service.dart';
import '../services/travel_intelligence_api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/auth_scope.dart';
import 'trip_widgets.dart';

class ItineraryPage extends StatefulWidget {
  const ItineraryPage({required this.api, required this.tripId, this.workflowSource, super.key});

  final TripApiService api;
  final String tripId;
  final AgentWorkflowSource? workflowSource;

  @override
  State<ItineraryPage> createState() => _ItineraryPageState();
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
    _workflowSource ??= widget.workflowSource ?? TravelIntelligenceApiService(
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
      final itinerary = results[0] as Itinerary?;
      final history = results[1] as List<ItineraryHistoryItem>;
      AgentWorkflow? workflow;
      try {
        workflow = await _workflowSource!.fetchWorkflow(widget.tripId);
      } on ApiException catch (error) {
        if (error.statusCode != 404) rethrow;
      }
      if (mounted) {
        setState(() {
          _itinerary = itinerary;
          _history = history;
          _workflow = workflow;
          _loading = false;
        });
      }
    } catch (error) {
      if (mounted) {
        setState(() {
          _error = error.toString();
          _loading = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (_error != null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Generated itinerary')),
        body: ErrorState(message: _error!, onRetry: _load),
      );
    }

    final itinerary = _itinerary;
    return Scaffold(
      appBar: AppBar(title: const Text('Generated itinerary')),
      body: itinerary == null
          ? const Center(
              child: Padding(
                padding: EdgeInsets.all(CeylonSpacing.lg),
                child: Text(
                  'No generated itinerary is available for this trip yet.',
                  textAlign: TextAlign.center,
                ),
              ),
            )
          : ListView(
              padding: const EdgeInsets.all(CeylonSpacing.md),
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'Itinerary',
                      style: Theme.of(context).textTheme.headlineMedium,
                    ),
                    TripStatusChip(status: itinerary.status),
                  ],
                ),
                const SizedBox(height: CeylonSpacing.sm),
                Text('Estimated cost: ${displayMoney(itinerary.totalEstimatedCost)}'),
                if (itinerary.createdAt != null) Text('Generated ${displayDate(itinerary.createdAt!)}'),
                const SizedBox(height: CeylonSpacing.md),
                _WorkflowSummary(workflow: _workflow),
                const SizedBox(height: CeylonSpacing.lg),
                ...itinerary.days.map(
                  (day) => Card(
                    child: ListTile(
                      title: Text('Day ${day.dayNumber}'),
                      subtitle: Text(
                        '${displayDate(day.date)}\n${day.items.length} activities',
                      ),
                      isThreeLine: true,
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () => context.push(
                        '/trips/${widget.tripId}/itinerary/day/${day.dayNumber}',
                        extra: day,
                      ),
                    ),
                  ),
                ),
                const SizedBox(height: CeylonSpacing.lg),
                Text('Previous versions', style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: CeylonSpacing.sm),
                if (_history.length <= 1)
                  const Text('No previous itinerary versions are available.')
                else
                  ..._history.skip(1).map((version) => Card(
                    child: ListTile(
                      title: Text(version.createdAt == null ? 'Itinerary version' : displayDate(version.createdAt!)),
                      subtitle: Text('${version.status} • ${displayMoney(version.totalEstimatedCost)}'),
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () async {
                        final selected = await widget.api.getItineraryVersion(widget.tripId, version.id);
                        if (mounted) setState(() => _itinerary = selected);
                      },
                    ),
                  )),
              ],
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
      return const Card(
        child: Padding(
          padding: EdgeInsets.all(CeylonSpacing.md),
          child: Text('Workflow not available yet.'),
        ),
      );
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(CeylonSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('AI workflow', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: CeylonSpacing.sm),
            Text('Status: ${_workflowStatusLabel(value.status)}'),
            if (value.safeMessage.isNotEmpty) Text(value.safeMessage),
            if (value.requiresApproval) const Text('Approval required'),
            if (value.reviewStatus != null) Text('Review: ${value.reviewStatus}'),
            const SizedBox(height: CeylonSpacing.sm),
            ...([...value.stages]..sort((a, b) => a.sequence.compareTo(b.sequence))).map(
              (stage) => Padding(
                padding: const EdgeInsets.only(bottom: CeylonSpacing.sm),
                child: Text(
                  '${stage.agentRole}: ${stage.status}${stage.summary.isEmpty ? '' : ' · ${stage.summary}'}',
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

String _workflowStatusLabel(AgentWorkflowStatus status) => switch (status) {
  AgentWorkflowStatus.awaitingApproval => 'Awaiting approval',
  AgentWorkflowStatus.failedSafe => 'Failed safely',
  AgentWorkflowStatus.pending => 'Pending',
  AgentWorkflowStatus.running => 'Running',
  AgentWorkflowStatus.completed => 'Completed',
  AgentWorkflowStatus.cancelled => 'Cancelled',
  AgentWorkflowStatus.unknown => 'Unknown',
};
