import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../config/api_config.dart';
import '../models/attraction_model.dart';
import '../theme/app_theme.dart';
import '../utils/attraction_images.dart';

class AttractionCard extends StatelessWidget {
  const AttractionCard({
    required this.attraction,
    this.onFavoriteChanged,
    this.onTap,
    super.key,
  });

  final AttractionModel attraction;
  final VoidCallback? onFavoriteChanged;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => Card(
    clipBehavior: Clip.antiAlias,
    child: InkWell(
      onTap: onTap ?? () => context.push('/discover/${attraction.id}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _ImagePreview(attraction: attraction),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 14, 12, 16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: Text(
                        attraction.name,
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                    ),
                    if (onFavoriteChanged != null)
                      IconButton(
                        tooltip: attraction.isFavorite ? 'Remove favorite' : 'Add favorite',
                        onPressed: onFavoriteChanged,
                        icon: Icon(
                          attraction.isFavorite ? Icons.favorite : Icons.favorite_border,
                          color: attraction.isFavorite ? CeylonColors.error : CeylonColors.tea,
                        ),
                      ),
                  ],
                ),
                const SizedBox(height: 5),
                Text(
                  '${attraction.category?.name ?? 'Experience'} · ${attraction.district}',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
                const SizedBox(height: 8),
                Text(
                  attraction.price == 0 ? 'Free entry' : 'From LKR ${attraction.price.toStringAsFixed(2)}',
                  style: Theme.of(context).textTheme.labelLarge,
                ),
              ],
            ),
          ),
        ],
      ),
    ),
  );
}

class _ImagePreview extends StatelessWidget {
  const _ImagePreview({required this.attraction});
  final AttractionModel attraction;

  @override
  Widget build(BuildContext context) {
    final gallery = attractionGallery(attraction);
    final image = gallery.isNotEmpty
        ? gallery.first
        : (attraction.images.isNotEmpty ? attraction.images.first.imageUrl : null);
    final resolvedImage = ApiConfig.resolveImageUrl(image);
    return SizedBox(
      height: 150,
      width: double.infinity,
      child: resolvedImage.isEmpty
          ? const ColoredBox(
              color: CeylonColors.mint,
              child: Center(child: Icon(Icons.landscape_outlined, size: 46, color: CeylonColors.tea)),
            )
          : resolvedImage.startsWith('http')
              ? Image.network(
                  resolvedImage,
                  fit: BoxFit.cover,
                  semanticLabel: attraction.name,
                  errorBuilder: (_, __, ___) => const ColoredBox(
                    color: CeylonColors.mint,
                    child: Center(child: Icon(Icons.landscape_outlined, size: 46, color: CeylonColors.tea)),
                  ),
                )
              : Image.asset(
                  resolvedImage,
                  fit: BoxFit.cover,
                  semanticLabel: attraction.name,
                  errorBuilder: (_, __, ___) => const ColoredBox(
                    color: CeylonColors.mint,
                    child: Center(child: Icon(Icons.landscape_outlined, size: 46, color: CeylonColors.tea)),
                  ),
                ),
    );
  }
}
