import 'package:flutter/material.dart';

import '../models/trip_model.dart';
import '../theme/app_theme.dart';
import 'trip_widgets.dart';

class ItineraryDayPage extends StatelessWidget {
  const ItineraryDayPage({required this.day, super.key});
  final ItineraryDay day;
  @override
  Widget build(BuildContext context) => Scaffold(appBar: AppBar(title: Text('Day ${day.dayNumber}')), body: Center(child: ConstrainedBox(constraints: const BoxConstraints(maxWidth: 760), child: ListView(padding: const EdgeInsets.fromLTRB(CeylonSpacing.md, CeylonSpacing.sm, CeylonSpacing.md, 48), children: [
    Text('Day ${day.dayNumber}', style: Theme.of(context).textTheme.headlineMedium), const SizedBox(height: 4), Text(displayDate(day.date), style: const TextStyle(color: CeylonColors.inkMuted)), const SizedBox(height: CeylonSpacing.xl),
    if (day.items.isEmpty) const SectionCard(child: Text('No experiences are stored for this day yet.')),
    ...day.items.asMap().entries.map((entry) => _ActivityCard(index: entry.key, item: entry.value)),
  ]))));
}

class _ActivityCard extends StatelessWidget {
  const _ActivityCard({required this.index, required this.item}); final int index; final ItineraryItem item;
  @override Widget build(BuildContext context) => Padding(padding: const EdgeInsets.only(bottom: CeylonSpacing.md), child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [Column(children: [Container(width: 30, height: 30, decoration: const BoxDecoration(color: CeylonColors.forest, shape: BoxShape.circle), child: Center(child: Text('${index + 1}', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800)))), if (index > 0) Container(width: 2, height: 150, color: CeylonColors.mint)]), const SizedBox(width: CeylonSpacing.sm), Expanded(child: SectionCard(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text('${item.startTime} – ${item.endTime}', style: const TextStyle(color: CeylonColors.tea, fontWeight: FontWeight.w800)), const SizedBox(height: 7), Text('Planned experience', style: Theme.of(context).textTheme.titleMedium), if (item.notes?.isNotEmpty == true) Padding(padding: const EdgeInsets.only(top: 8), child: Text(item.notes!)), const SizedBox(height: 10), Text(displayMoney(item.estimatedCost), style: const TextStyle(fontWeight: FontWeight.w700, color: CeylonColors.inkMuted))])))]));
}
