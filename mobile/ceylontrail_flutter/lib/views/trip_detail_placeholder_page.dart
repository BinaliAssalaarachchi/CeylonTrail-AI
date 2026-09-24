import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

class TripDetailPlaceholderPage extends StatelessWidget {
  const TripDetailPlaceholderPage({required this.tripId, super.key});

  final String tripId;

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Trip details')),
    body: Padding(
      padding: const EdgeInsets.all(CeylonSpacing.lg),
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(CeylonSpacing.lg),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Your trip', style: Theme.of(context).textTheme.headlineMedium),
              const SizedBox(height: CeylonSpacing.sm),
              const Text('Itinerary details will appear here in the next feature phase.'),
              const SizedBox(height: CeylonSpacing.md),
              const Text('No itinerary data has been loaded yet.', style: TextStyle(color: CeylonColors.inkMuted)),
            ],
          ),
        ),
      ),
    ),
  );
}
