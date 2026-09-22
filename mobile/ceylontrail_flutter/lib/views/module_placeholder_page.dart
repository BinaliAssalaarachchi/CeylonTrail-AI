import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import '../widgets/brand_mark.dart';

class ModulePlaceholderPage extends StatelessWidget {
  const ModulePlaceholderPage({
    required this.title,
    required this.description,
    required this.icon,
    super.key,
  });

  final String title;
  final String description;
  final IconData icon;

  @override
  Widget build(BuildContext context) => SafeArea(
    child: SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(
        CeylonSpacing.lg,
        CeylonSpacing.lg,
        CeylonSpacing.lg,
        CeylonSpacing.xl,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const BrandLockup(compact: true),
          const SizedBox(height: 52),
          Container(
            padding: const EdgeInsets.all(CeylonSpacing.lg),
            decoration: BoxDecoration(
              color: CeylonColors.forest,
              borderRadius: BorderRadius.circular(CeylonRadii.card),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(icon, color: CeylonColors.mint, size: 34),
                const SizedBox(height: CeylonSpacing.md),
                Text(
                  title,
                  style: Theme.of(
                    context,
                  ).textTheme.headlineMedium?.copyWith(color: Colors.white),
                ),
                const SizedBox(height: CeylonSpacing.sm),
                Text(
                  description,
                  style: Theme.of(
                    context,
                  ).textTheme.bodyLarge?.copyWith(color: Colors.white70),
                ),
              ],
            ),
          ),
          const SizedBox(height: CeylonSpacing.lg),
          Text(
            'A shared foundation',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: CeylonSpacing.sm),
          Text(
            'This space is ready for the feature team that owns it. No sample business data is shown here.',
            style: Theme.of(context).textTheme.bodyLarge,
          ),
        ],
      ),
    ),
  );
}
