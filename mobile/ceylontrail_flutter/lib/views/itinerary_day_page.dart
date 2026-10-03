import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../models/trip_model.dart';
import '../theme/app_theme.dart';
import 'trip_widgets.dart';

class ItineraryDayPage extends StatelessWidget {
  const ItineraryDayPage({required this.day, super.key});
  final ItineraryDay day;

  @override
  Widget build(BuildContext context) {
    final cost = day.items.fold<double>(
      0,
      (sum, item) => sum + item.estimatedCost,
    );
    return Scaffold(
      appBar: AppBar(title: Text('Day ${day.dayNumber}')),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 800),
          child: ListView(
            padding: const EdgeInsets.fromLTRB(
              CeylonSpacing.md,
              CeylonSpacing.sm,
              CeylonSpacing.md,
              48,
            ),
            children: [
              SectionCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'DAY ${day.dayNumber}',
                      style: const TextStyle(
                        color: CeylonColors.teal,
                        fontWeight: FontWeight.w800,
                        letterSpacing: 1.2,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      displayDate(day.date),
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                    const SizedBox(height: 8),
                    Text(
                      '${day.items.length} ${day.items.length == 1 ? 'planned place' : 'planned places'}  •  ${displayMoney(cost)}',
                      style: const TextStyle(color: CeylonColors.inkMuted),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: CeylonSpacing.lg),
              if (day.items.isEmpty)
                const SectionCard(
                  child: Text('No activities are planned for this day yet.'),
                ),
              ...day.items.asMap().entries.map(
                (entry) => _TimelineItem(
                  index: entry.key,
                  total: day.items.length,
                  item: entry.value,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _TimelineItem extends StatelessWidget {
  const _TimelineItem({
    required this.index,
    required this.total,
    required this.item,
  });
  final int index, total;
  final ItineraryItem item;

  @override
  Widget build(BuildContext context) {
    final location = [
      item.address,
      item.district,
    ].whereType<String>().where((value) => value.trim().isNotEmpty).join(', ');
    final title = item.attractionName?.trim().isNotEmpty == true
        ? item.attractionName!
        : 'Planned experience';
    final hasCoordinates = item.latitude != null && item.longitude != null;
    return Padding(
      padding: const EdgeInsets.only(bottom: CeylonSpacing.md),
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SizedBox(
              width: 38,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    width: 32,
                    height: 32,
                    decoration: const BoxDecoration(
                      color: CeylonColors.forest,
                      shape: BoxShape.circle,
                    ),
                    child: Center(
                      child: Text(
                        '${index + 1}',
                        style: const TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ),
                  ),
                  if (index < total - 1)
                    Expanded(
                      child: Center(
                        child: Container(width: 2, color: CeylonColors.mint),
                      ),
                    ),
                ],
              ),
            ),
            const SizedBox(width: CeylonSpacing.sm),
            Expanded(
              child: SectionCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${item.startTime} – ${item.endTime}',
                      style: const TextStyle(
                        color: CeylonColors.tea,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 7),
                    Text(title, style: Theme.of(context).textTheme.titleLarge),
                    if (item.category?.trim().isNotEmpty == true)
                      Padding(
                        padding: const EdgeInsets.only(top: 5),
                        child: Text(
                          item.category!,
                          style: const TextStyle(color: CeylonColors.inkMuted),
                        ),
                      ),
                    if (location.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 5),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Icon(
                              Icons.location_on_outlined,
                              size: 18,
                              color: CeylonColors.teal,
                            ),
                            const SizedBox(width: 5),
                            Expanded(
                              child: Text(
                                location,
                                style: const TextStyle(
                                  color: CeylonColors.inkMuted,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    if (item.description?.trim().isNotEmpty == true)
                      Padding(
                        padding: const EdgeInsets.only(top: 10),
                        child: Text(item.description!),
                      ),
                    if (hasCoordinates)
                      Align(
                        alignment: Alignment.centerLeft,
                        child: TextButton.icon(
                          onPressed: () => _openMap(context),
                          icon: const Icon(Icons.map_outlined, size: 18),
                          label: const Text('View on map'),
                          style: TextButton.styleFrom(padding: EdgeInsets.zero),
                        ),
                      ),
                    if (item.notes?.trim().isNotEmpty == true)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text(
                          item.notes!,
                          style: const TextStyle(color: CeylonColors.inkMuted),
                        ),
                      ),
                    const SizedBox(height: 10),
                    Text(
                      displayMoney(item.estimatedCost),
                      style: const TextStyle(
                        fontWeight: FontWeight.w800,
                        color: CeylonColors.inkMuted,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _openMap(BuildContext context) async {
    final uri = Uri.parse(
      'https://www.google.com/maps/search/?api=1&query=${item.latitude},${item.longitude}',
    );
    if (!await launchUrl(uri, mode: LaunchMode.externalApplication) &&
        context.mounted) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Could not open the map.')));
    }
  }
}
