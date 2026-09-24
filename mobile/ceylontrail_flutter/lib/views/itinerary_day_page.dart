import 'package:flutter/material.dart';

import '../models/trip_model.dart';
import '../theme/app_theme.dart';
import 'trip_widgets.dart';

class ItineraryDayPage extends StatelessWidget {
  const ItineraryDayPage({required this.day, super.key});
  final ItineraryDay day;

  @override
  Widget build(BuildContext context) => Scaffold(appBar: AppBar(title: Text('Day ${day.dayNumber}')), body: ListView(padding: const EdgeInsets.all(CeylonSpacing.md), children: [Text(displayDate(day.date), style: Theme.of(context).textTheme.headlineMedium), const SizedBox(height: CeylonSpacing.md), if (day.items.isEmpty) const Text('No activities are stored for this day.'), ...day.items.map((item) => Card(margin: const EdgeInsets.only(bottom: CeylonSpacing.sm), child: Padding(padding: const EdgeInsets.all(CeylonSpacing.md), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text('${item.startTime} – ${item.endTime}', style: Theme.of(context).textTheme.titleMedium), const SizedBox(height: 8), Text('Attraction reference: ${item.attractionId}'), const SizedBox(height: 8), Text(displayMoney(item.estimatedCost)), if (item.notes?.isNotEmpty == true) Padding(padding: const EdgeInsets.only(top: 8), child: Text(item.notes!))]))))]));
}
