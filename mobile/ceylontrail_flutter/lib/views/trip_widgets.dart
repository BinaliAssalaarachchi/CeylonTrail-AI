import 'package:flutter/material.dart';

import '../models/trip_model.dart';
import '../theme/app_theme.dart';

String displayDate(DateTime date) => '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year}';
String displayMoney(double value) => 'Rs. ${value.toStringAsFixed(2)}';

Color statusColor(String status) {
  switch (status) {
    case 'Completed': return CeylonColors.teal;
    case 'Cancelled': return CeylonColors.error;
    case 'Planned': return CeylonColors.tea;
    default: return CeylonColors.amber;
  }
}

class TripStatusChip extends StatelessWidget {
  const TripStatusChip({required this.status, super.key});
  final String status;

  @override
  Widget build(BuildContext context) => Chip(
    label: Text(status),
    labelStyle: TextStyle(color: statusColor(status), fontWeight: FontWeight.w700),
    backgroundColor: statusColor(status).withValues(alpha: .12),
    side: BorderSide.none,
  );
}

class ErrorState extends StatelessWidget {
  const ErrorState({required this.message, required this.onRetry, super.key});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(CeylonSpacing.lg),
      child: Column(mainAxisSize: MainAxisSize.min, children: [
        const Icon(Icons.cloud_off_outlined, size: 48, color: CeylonColors.inkMuted),
        const SizedBox(height: CeylonSpacing.md),
        Text(message, textAlign: TextAlign.center),
        const SizedBox(height: CeylonSpacing.md),
        OutlinedButton(onPressed: onRetry, child: const Text('Try again')),
      ]),
    ),
  );
}

class PreferenceList extends StatelessWidget {
  const PreferenceList({required this.preferences, super.key});
  final List<TripPreference> preferences;

  @override
  Widget build(BuildContext context) => preferences.isEmpty
      ? const Text('No preferences added yet.')
      : Wrap(spacing: 8, runSpacing: 8, children: preferences.map((p) => Chip(label: Text('${p.preferenceType}: ${p.value}'))).toList());
}
