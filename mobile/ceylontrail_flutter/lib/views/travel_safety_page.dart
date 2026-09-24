import 'package:flutter/material.dart';

import '../models/travel_intelligence_outcome_model.dart';
import '../services/api_client.dart';
import '../services/travel_intelligence_api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/auth_scope.dart';

class TravelSafetyPage extends StatefulWidget {
  const TravelSafetyPage({required this.tripId, this.source, super.key});

  final String tripId;
  final TravelIntelligenceOutcomeSource? source;

  @override
  State<TravelSafetyPage> createState() => _TravelSafetyPageState();
}

class _TravelSafetyPageState extends State<TravelSafetyPage> {
  TravelIntelligenceOutcomeSource? _source;
  TravelIntelligenceOutcome? _outcome;
  String? _error;
  bool _loading = true;
  bool _loaded = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _source ??= widget.source ?? TravelIntelligenceApiService(
      ApiClient(storage: AuthScope.of(context).storage),
    );
    if (!_loaded) {
      _loaded = true;
      _load();
    }
  }

  Future<void> _load() async {
    if (mounted) setState(() { _loading = true; _error = null; });
    try {
      final outcome = await _source!.fetchLatest(widget.tripId);
      if (mounted) setState(() { _outcome = outcome; _loading = false; });
    } on ApiException catch (error) {
      if (mounted) setState(() { _error = error.message; _loading = false; });
    } catch (_) {
      if (mounted) setState(() { _error = 'Travel safety information is unavailable. Please try again.'; _loading = false; });
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Travel Safety')),
    body: RefreshIndicator(
      onRefresh: _load,
      child: _loading
          ? ListView(children: const [SizedBox(height: 240), Center(child: CircularProgressIndicator())])
          : _error != null
              ? ListView(children: [_StateMessage(icon: Icons.cloud_off_outlined, title: 'Travel safety is unavailable', message: _error!, action: _load)])
              : _outcome == null
                  ? ListView(children: [_StateMessage(icon: Icons.shield_outlined, title: 'Travel safety assessment not available yet', message: 'Once your trip is assessed, safety recommendations and review status will appear here.', action: _load)])
                  : ListView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      padding: const EdgeInsets.fromLTRB(CeylonSpacing.md, CeylonSpacing.sm, CeylonSpacing.md, CeylonSpacing.xl),
                      children: [_OutcomeView(outcome: _outcome!)],
                    ),
    ),
  );
}

class _OutcomeView extends StatelessWidget {
  const _OutcomeView({required this.outcome});
  final TravelIntelligenceOutcome outcome;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text('Travel Safety', style: Theme.of(context).textTheme.displaySmall),
      const SizedBox(height: CeylonSpacing.md),
      _RiskCard(outcome: outcome),
      if (outcome.summary.isNotEmpty) ...[
        const SizedBox(height: CeylonSpacing.md),
        _SectionCard(title: 'Why this recommendation', children: [Text(outcome.summary)]),
      ],
      if (outcome.recommendations.isNotEmpty) ...[
        const SizedBox(height: CeylonSpacing.md),
        _SectionCard(title: 'AI Recommendation', children: outcome.recommendations.map((item) => _Recommendation(item: item)).toList()),
      ],
      if (outcome.affectedItems.isNotEmpty) ...[
        const SizedBox(height: CeylonSpacing.md),
        _SectionCard(title: 'Affected Trip Items', children: outcome.affectedItems.map((item) => _AffectedItem(item: item)).toList()),
      ],
      if (outcome.alternatives.isNotEmpty) ...[
        const SizedBox(height: CeylonSpacing.md),
        _SectionCard(title: 'Suggested Alternatives', children: outcome.alternatives.map((item) => _Alternative(item: item)).toList()),
      ],
      if (outcome.safeWindows.isNotEmpty) ...[
        const SizedBox(height: CeylonSpacing.md),
        _SectionCard(title: 'Safer Time Windows', children: outcome.safeWindows.map((item) => _SafeWindow(item: item)).toList()),
      ],
      const SizedBox(height: CeylonSpacing.md),
      _ReviewCard(status: outcome.reviewStatus),
      if (outcome.assessedAt != null) ...[
        const SizedBox(height: CeylonSpacing.sm),
        Text('Assessed ${_dateTime(outcome.assessedAt)}', style: Theme.of(context).textTheme.bodySmall),
      ],
    ],
  );
}

class _RiskCard extends StatelessWidget {
  const _RiskCard({required this.outcome});
  final TravelIntelligenceOutcome outcome;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(CeylonSpacing.md),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text('${_label(outcome.riskLevel)} risk', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: CeylonSpacing.sm),
        Text(outcome.isFeasible ? 'This trip is currently feasible.' : 'This trip needs safety changes before proceeding.'),
        const SizedBox(height: CeylonSpacing.sm),
        Text('Recommended action: ${_label(outcome.recommendedAction)}'),
      ]),
    ),
  );
}

class _SectionCard extends StatelessWidget {
  const _SectionCard({required this.title, required this.children});
  final String title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(CeylonSpacing.md),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(title, style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: CeylonSpacing.sm),
        ...children.expand((child) => [child, const SizedBox(height: CeylonSpacing.sm)]),
      ]),
    ),
  );
}

class _Recommendation extends StatelessWidget {
  const _Recommendation({required this.item});
  final TravelRecommendation item;
  @override
  Widget build(BuildContext context) => Text('${_label(item.action)}: ${item.explanation}');
}

class _AffectedItem extends StatelessWidget {
  const _AffectedItem({required this.item});
  final TravelAffectedItem item;
  @override
  Widget build(BuildContext context) {
    final name = item.title?.isNotEmpty == true ? item.title! : item.district?.isNotEmpty == true ? item.district! : 'Trip item';
    return Text('${item.isBlocking ? 'Important: ' : ''}$name');
  }
}

class _Alternative extends StatelessWidget {
  const _Alternative({required this.item});
  final TravelAlternative item;
  @override
  Widget build(BuildContext context) => Text('${_label(item.action)}: ${item.rationale}');
}

class _SafeWindow extends StatelessWidget {
  const _SafeWindow({required this.item});
  final TravelSafeWindow item;
  @override
  Widget build(BuildContext context) => Text('${_dateTime(item.proposedStart)} – ${_dateTime(item.proposedEnd)}${item.reason.isEmpty ? '' : ': ${item.reason}'}');
}

class _ReviewCard extends StatelessWidget {
  const _ReviewCard({required this.status});
  final TouristReviewStatus status;
  @override
  Widget build(BuildContext context) => Card(
    child: ListTile(
      leading: Icon(_reviewIcon(status), color: CeylonColors.tea),
      title: Text(_reviewTitle(status)),
      subtitle: Text(_reviewMessage(status)),
    ),
  );
}

class _StateMessage extends StatelessWidget {
  const _StateMessage({required this.icon, required this.title, required this.message, required this.action});
  final IconData icon;
  final String title;
  final String message;
  final VoidCallback action;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.all(CeylonSpacing.xl),
    child: Column(mainAxisSize: MainAxisSize.min, children: [
      Icon(icon, color: CeylonColors.tea, size: 42),
      const SizedBox(height: CeylonSpacing.md),
      Text(title, style: Theme.of(context).textTheme.titleLarge, textAlign: TextAlign.center),
      const SizedBox(height: CeylonSpacing.sm),
      Text(message, textAlign: TextAlign.center),
      const SizedBox(height: CeylonSpacing.md),
      OutlinedButton(onPressed: action, child: const Text('Refresh')),
    ]),
  );
}

String _label(Object value) => value.toString().split('.').last.replaceAllMapped(RegExp(r'([a-z])([A-Z])'), (match) => '${match.group(1)} ${match.group(2)}');
String _dateTime(DateTime? value) => value == null ? 'Time unavailable' : '${value.day}/${value.month}/${value.year}';
IconData _reviewIcon(TouristReviewStatus status) => switch (status) {
  TouristReviewStatus.notRequired => Icons.check_circle_outline,
  TouristReviewStatus.approvalRequired || TouristReviewStatus.pending => Icons.hourglass_empty,
  TouristReviewStatus.approved => Icons.verified_outlined,
  TouristReviewStatus.rejected => Icons.cancel_outlined,
  TouristReviewStatus.unknown => Icons.info_outline,
};
String _reviewTitle(TouristReviewStatus status) => switch (status) {
  TouristReviewStatus.notRequired => 'Human review not required',
  TouristReviewStatus.approvalRequired || TouristReviewStatus.pending => 'Awaiting Travel Coordinator Review',
  TouristReviewStatus.approved => 'Approved by Travel Coordinator',
  TouristReviewStatus.rejected => 'Not Approved',
  TouristReviewStatus.unknown => 'Review status unavailable',
};
String _reviewMessage(TouristReviewStatus status) => switch (status) {
  TouristReviewStatus.notRequired => 'This safety recommendation does not require human review.',
  TouristReviewStatus.approvalRequired || TouristReviewStatus.pending => 'The recommendation has not yet been approved. Your itinerary has not been changed automatically.',
  TouristReviewStatus.approved => 'The safety recommendation has been reviewed and approved.',
  TouristReviewStatus.rejected => 'The recommendation was reviewed but was not approved.',
  TouristReviewStatus.unknown => 'The review status could not be determined.',
};
