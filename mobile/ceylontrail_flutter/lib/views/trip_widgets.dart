import 'package:flutter/material.dart';

import '../models/trip_model.dart';
import '../theme/app_theme.dart';

String displayDate(DateTime date) => '${date.day} ${_months[date.month - 1]} ${date.year}';
String displayMoney(double value) {
  final rounded = value.round();
  final text = rounded.toString().replaceAllMapped(RegExp(r'(?<=\d)(?=(\d{3})+(?!\d))'), (_) => ',');
  return 'LKR $text';
}

const _months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

Color statusColor(String status) {
  switch (status.toLowerCase()) {
    case 'completed': return CeylonColors.teal;
    case 'cancelled': return CeylonColors.error;
    case 'planned': return CeylonColors.tea;
    default: return CeylonColors.amber;
  }
}

String friendlyStatus(String status) {
  switch (status.toLowerCase()) {
    case 'completed': return 'Completed';
    case 'cancelled': return 'Cancelled';
    case 'planned': case 'draft': return 'Planning';
    case 'pendinghumanapproval': case 'pending approval': return 'Being reviewed';
    default: return status.isEmpty ? 'Planning' : status;
  }
}

String tripDuration(Trip trip) {
  final days = trip.endDate.difference(trip.startDate).inDays + 1;
  return '$days ${days == 1 ? 'day' : 'days'}';
}

class TripStatusChip extends StatelessWidget {
  const TripStatusChip({required this.status, super.key});
  final String status;
  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 7),
    decoration: BoxDecoration(color: statusColor(status).withValues(alpha: .14), borderRadius: BorderRadius.circular(CeylonRadii.pill)),
    child: Text(friendlyStatus(status), style: TextStyle(color: statusColor(status), fontSize: 12, fontWeight: FontWeight.w800)),
  );
}

class ErrorState extends StatelessWidget {
  const ErrorState({required this.message, required this.onRetry, super.key});
  final String message;
  final VoidCallback onRetry;
  @override
  Widget build(BuildContext context) => Center(child: Padding(
    padding: const EdgeInsets.all(CeylonSpacing.lg),
    child: Column(mainAxisSize: MainAxisSize.min, children: [
      const Icon(Icons.cloud_off_outlined, size: 48, color: CeylonColors.inkMuted), const SizedBox(height: CeylonSpacing.md),
      Text(message, textAlign: TextAlign.center), const SizedBox(height: CeylonSpacing.md),
      OutlinedButton(onPressed: onRetry, child: const Text('Try again')),
    ]),
  ));
}

class PreferenceList extends StatelessWidget {
  const PreferenceList({required this.preferences, super.key});
  final List<TripPreference> preferences;
  @override
  Widget build(BuildContext context) => preferences.isEmpty
      ? const Text('No preferences added yet.')
      : Column(crossAxisAlignment: CrossAxisAlignment.start, children: preferences.map((p) => Padding(
          padding: const EdgeInsets.only(bottom: CeylonSpacing.sm),
          child: Container(width: double.infinity, padding: const EdgeInsets.all(CeylonSpacing.md), decoration: BoxDecoration(color: CeylonColors.surfaceSoft, borderRadius: BorderRadius.circular(CeylonRadii.field)), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(p.preferenceType, style: const TextStyle(color: CeylonColors.inkMuted, fontSize: 12, fontWeight: FontWeight.w800)), const SizedBox(height: 4), Text(p.value),
          ])),
        )).toList());
}

class SectionCard extends StatelessWidget {
  const SectionCard({required this.child, this.padding = const EdgeInsets.all(CeylonSpacing.md), super.key});
  final Widget child;
  final EdgeInsets padding;
  @override
  Widget build(BuildContext context) => Card(child: Padding(padding: padding, child: child));
}
