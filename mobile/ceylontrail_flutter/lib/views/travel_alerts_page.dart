import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/travel_alert_model.dart';
import '../services/api_client.dart';
import '../services/travel_alert_api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/auth_scope.dart';

class TravelAlertsPage extends StatefulWidget {
  const TravelAlertsPage({super.key});
  @override
  State<TravelAlertsPage> createState() => _TravelAlertsPageState();
}

class _TravelAlertsPageState extends State<TravelAlertsPage> {
  TravelAlertApiService? _service;
  List<TravelAlert> _alerts = const [];
  String? _error;
  bool _loading = true;
  String? _district;
  TravelAlertSeverity? _severity;
  TravelAlertStatus? _status = TravelAlertStatus.active;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _service ??= TravelAlertApiService(ApiClient(storage: AuthScope.of(context).storage));
    if (_loading) _load();
  }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final result = await _service!.fetchAlerts(
        district: _district, severity: _severity, status: _status,
      );
      if (!mounted) return;
      setState(() { _alerts = result.items; _loading = false; });
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() { _error = error.message; _loading = false; });
    }
  }

  Future<void> _chooseDistrict() async {
    final controller = TextEditingController(text: _district ?? '');
    final value = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Filter by district'),
        content: TextField(
          controller: controller,
          autofocus: true,
          textCapitalization: TextCapitalization.words,
          decoration: const InputDecoration(hintText: 'e.g. Kandy'),
          onSubmitted: (value) => Navigator.pop(context, value.trim()),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, ''), child: const Text('Clear')),
          FilledButton(onPressed: () => Navigator.pop(context, controller.text.trim()), child: const Text('Apply')),
        ],
      ),
    );
    controller.dispose();
    if (value == null || !mounted) return;
    setState(() => _district = value.isEmpty ? null : value);
    await _load();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Travel advisories')),
      body: RefreshIndicator(
        onRefresh: _load,
        child: CustomScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          slivers: [
            SliverToBoxAdapter(
              child: _FilterBar(
                onDistrict: _chooseDistrict,
                severity: _severity,
                onSeverity: (value) { setState(() => _severity = value); _load(); },
                status: _status,
                onStatus: (value) { setState(() => _status = value); _load(); },
              ),
            ),
            if (_loading)
              const SliverFillRemaining(
                hasScrollBody: false,
                child: Center(child: CircularProgressIndicator()),
              )
            else if (_error != null)
              SliverFillRemaining(
                hasScrollBody: false,
                child: _StateMessage(
                  icon: Icons.cloud_off_outlined,
                  title: 'Advisories are unavailable',
                  message: _error!,
                  action: _load,
                ),
              )
            else if (_alerts.isEmpty)
              const SliverFillRemaining(
                hasScrollBody: false,
                child: _StateMessage(
                  icon: Icons.check_circle_outline,
                  title: 'No active advisories',
                  message: 'There are no current travel alerts matching your filters.',
                ),
              )
            else
              SliverPadding(
                padding: const EdgeInsets.fromLTRB(CeylonSpacing.md, CeylonSpacing.sm, CeylonSpacing.md, CeylonSpacing.xl),
                sliver: SliverList.builder(
                  itemCount: _alerts.length,
                  itemBuilder: (context, index) => Padding(
                    padding: const EdgeInsets.only(bottom: CeylonSpacing.md),
                    child: _AlertCard(alert: _alerts[index]),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class TravelAlertDetailPage extends StatefulWidget {
  const TravelAlertDetailPage({required this.id, super.key});
  final String id;
  @override
  State<TravelAlertDetailPage> createState() => _TravelAlertDetailPageState();
}

class _TravelAlertDetailPageState extends State<TravelAlertDetailPage> {
  TravelAlert? _alert;
  String? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_alert == null && _error == null) _load();
  }

  Future<void> _load() async {
    try {
      final service = TravelAlertApiService(ApiClient(storage: AuthScope.of(context).storage));
      final alert = await service.fetchAlert(widget.id);
      if (mounted) setState(() => _alert = alert);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_error != null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Travel advisory')),
        body: _StateMessage(
          icon: Icons.cloud_off_outlined,
          title: 'Unable to load advisory',
          message: _error!,
          action: _load,
        ),
      );
    }
    final alert = _alert;
    if (alert == null) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    return Scaffold(
      appBar: AppBar(title: const Text('Travel advisory')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(CeylonSpacing.lg, CeylonSpacing.sm, CeylonSpacing.lg, CeylonSpacing.xl),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _SeverityBadge(severity: alert.severity),
            const SizedBox(height: CeylonSpacing.md),
            Text(alert.title, style: Theme.of(context).textTheme.displaySmall),
            const SizedBox(height: CeylonSpacing.md),
            Wrap(
              spacing: CeylonSpacing.sm,
              runSpacing: CeylonSpacing.sm,
              children: [
                _InfoPill(icon: Icons.location_on_outlined, text: alert.district),
                _InfoPill(icon: Icons.category_outlined, text: _label(alert.alertType)),
                _InfoPill(icon: Icons.info_outline, text: _label(alert.status)),
              ],
            ),
            const SizedBox(height: CeylonSpacing.lg),
            Text(alert.description, style: Theme.of(context).textTheme.bodyLarge),
            const SizedBox(height: CeylonSpacing.lg),
            _DetailsCard(alert: alert),
          ],
        ),
      ),
    );
  }
}

class _FilterBar extends StatelessWidget {
  const _FilterBar({required this.onDistrict, required this.severity, required this.onSeverity, required this.status, required this.onStatus});
  final VoidCallback onDistrict;
  final TravelAlertSeverity? severity;
  final ValueChanged<TravelAlertSeverity?> onSeverity;
  final TravelAlertStatus? status;
  final ValueChanged<TravelAlertStatus?> onStatus;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(CeylonSpacing.md, CeylonSpacing.sm, CeylonSpacing.md, CeylonSpacing.sm),
      child: Wrap(
        spacing: CeylonSpacing.sm,
        runSpacing: CeylonSpacing.sm,
        children: [
          OutlinedButton.icon(
            onPressed: onDistrict,
            icon: const Icon(Icons.location_on_outlined, size: 18),
            label: const Text('District'),
          ),
          DropdownButton<TravelAlertSeverity?>(
            value: severity,
            hint: const Text('Severity'),
            items: [
              const DropdownMenuItem(value: null, child: Text('All severities')),
              ...TravelAlertSeverity.values.map((value) => DropdownMenuItem(value: value, child: Text(_label(value)))),
            ],
            onChanged: onSeverity,
          ),
          DropdownButton<TravelAlertStatus?>(
            value: status,
            hint: const Text('Status'),
            items: [
              const DropdownMenuItem(value: null, child: Text('All statuses')),
              ...TravelAlertStatus.values.map((value) => DropdownMenuItem(value: value, child: Text(_label(value)))),
            ],
            onChanged: onStatus,
          ),
        ],
      ),
    );
  }
}

class _AlertCard extends StatelessWidget {
  const _AlertCard({required this.alert});
  final TravelAlert alert;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(CeylonRadii.card),
        onTap: () => context.push('/travel-alerts/${alert.id}'),
        child: Padding(
          padding: const EdgeInsets.all(CeylonSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _SeverityBadge(severity: alert.severity),
              const SizedBox(height: CeylonSpacing.sm),
              Text(alert.title, style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: CeylonSpacing.sm),
              Row(
                children: [
                  const Icon(Icons.location_on_outlined, size: 17, color: CeylonColors.tea),
                  const SizedBox(width: 4),
                  Text(alert.district),
                  const Spacer(),
                  Text(_label(alert.alertType), style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
              const SizedBox(height: CeylonSpacing.sm),
              Text(_dateRange(alert), style: Theme.of(context).textTheme.bodySmall),
              const SizedBox(height: CeylonSpacing.sm),
              Row(
                children: [
                  Text(_label(alert.status), style: Theme.of(context).textTheme.labelMedium?.copyWith(color: CeylonColors.tea, fontWeight: FontWeight.w800)),
                  const Spacer(),
                  const Icon(Icons.chevron_right, color: CeylonColors.inkMuted),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _DetailsCard extends StatelessWidget {
  const _DetailsCard({required this.alert});
  final TravelAlert alert;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(CeylonSpacing.md),
        child: Column(
          children: [
            _DetailRow(label: 'When', value: _dateRange(alert)),
            _DetailRow(label: 'Status', value: _label(alert.status)),
            if (alert.source?.isNotEmpty ?? false)
              _DetailRow(label: 'Source', value: alert.source!),
          ],
        ),
      ),
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: CeylonSpacing.sm),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(width: 72, child: Text(label, style: Theme.of(context).textTheme.labelLarge)),
          Expanded(child: Text(value)),
        ],
      ),
    );
  }
}

class _SeverityBadge extends StatelessWidget {
  const _SeverityBadge({required this.severity});
  final TravelAlertSeverity severity;

  @override
  Widget build(BuildContext context) {
    final color = _severityColor(severity);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(color: color.withValues(alpha: .13), borderRadius: BorderRadius.circular(CeylonRadii.pill)),
      child: Text('${_label(severity)} priority', style: TextStyle(color: color, fontWeight: FontWeight.w800, fontSize: 12)),
    );
  }
}

class _InfoPill extends StatelessWidget {
  const _InfoPill({required this.icon, required this.text});
  final IconData icon;
  final String text;
  @override
  Widget build(BuildContext context) => Chip(avatar: Icon(icon, size: 16, color: CeylonColors.tea), label: Text(text));
}

class _StateMessage extends StatelessWidget {
  const _StateMessage({required this.icon, required this.title, required this.message, this.action});
  final IconData icon;
  final String title;
  final String message;
  final VoidCallback? action;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(CeylonSpacing.xl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, color: CeylonColors.tea, size: 42),
            const SizedBox(height: CeylonSpacing.md),
            Text(title, style: Theme.of(context).textTheme.titleLarge, textAlign: TextAlign.center),
            const SizedBox(height: CeylonSpacing.sm),
            Text(message, textAlign: TextAlign.center),
            if (action != null) ...[
              const SizedBox(height: CeylonSpacing.md),
              OutlinedButton(onPressed: action, child: const Text('Try again')),
            ],
          ],
        ),
      ),
    );
  }
}

String _label(Object value) {
  final raw = value.toString().split('.').last;
  return raw.replaceAllMapped(RegExp(r'([a-z])([A-Z])'), (match) => '${match.group(1)} ${match.group(2)}');
}

String _dateRange(TravelAlert alert) => '${_date(alert.startDateTime)} – ${_date(alert.endDateTime)}';
String _date(DateTime? value) => value == null ? 'Date unavailable' : '${value.day}/${value.month}/${value.year}';
Color _severityColor(TravelAlertSeverity severity) => switch (severity) {
  TravelAlertSeverity.low => CeylonColors.tea,
  TravelAlertSeverity.medium => CeylonColors.amber,
  TravelAlertSeverity.high => const Color(0xFFC66A00),
  TravelAlertSeverity.critical => CeylonColors.error,
};
