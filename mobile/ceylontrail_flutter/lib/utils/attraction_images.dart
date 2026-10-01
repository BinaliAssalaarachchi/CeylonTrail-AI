import '../models/attraction_model.dart';

const Map<String, List<String>> attractionGalleryAssets = {
  'Arugam Bay Coastal Surf Lesson': [
    'assets/images/destinations/arugam_bay/01.jpg',
    'assets/images/destinations/arugam_bay/02.jpg',
    'assets/images/destinations/arugam_bay/03.jpg',
  ],
  'Bentota River Kayak': [
    'assets/images/destinations/bentota/01.jpg',
    'assets/images/destinations/bentota/02.jpg',
    'assets/images/destinations/bentota/03.jpg',
  ],
  'Ella Tea Country Hike': [
    'assets/images/destinations/ella/01.jpg',
    'assets/images/destinations/ella/02.jpg',
    'assets/images/destinations/ella/03.jpg',
  ],
  'Galle Fort Heritage Walk': [
    'assets/images/destinations/galle_fort/01.jpg',
    'assets/images/destinations/galle_fort/02.jpg',
    'assets/images/destinations/galle_fort/03.jpg',
  ],
  'Kandy Lake and Temple Walk': [
    'assets/images/destinations/kandy/01.jpg',
    'assets/images/destinations/kandy/02.jpg',
    'assets/images/destinations/kandy/03.jpg',
  ],
  'Sigiriya Heritage Sunrise Trail': [
    'assets/images/destinations/sigiriya/01.jpg',
    'assets/images/destinations/sigiriya/02.jpg',
    'assets/images/destinations/sigiriya/03.jpg',
  ],
  'Sinharaja Rainforest Nature Walk': [
    'assets/images/destinations/sinharaja/01.jpg',
    'assets/images/destinations/sinharaja/02.jpg',
    'assets/images/destinations/sinharaja/03.jpg',
  ],
  'Udawalawe Wildlife Safari': [
    'assets/images/destinations/udawalawe/01.jpg',
    'assets/images/destinations/udawalawe/02.jpg',
    'assets/images/destinations/udawalawe/03.jpg',
  ],
};

List<String> attractionGallery(AttractionModel attraction) =>
    attractionGalleryAssets[attraction.name] ?? const [];

String? destinationHeroAsset(String destination) {
  final normalized = destination.trim().toLowerCase();
  if (normalized.contains('sigiriya')) {
    return attractionGalleryAssets['Sigiriya Heritage Sunrise Trail']!.first;
  }
  if (normalized.contains('arugam bay')) {
    return attractionGalleryAssets['Arugam Bay Coastal Surf Lesson']!.first;
  }
  if (normalized.contains('bentota')) {
    return attractionGalleryAssets['Bentota River Kayak']!.first;
  }
  if (normalized.contains('ella') || normalized.contains('tea country')) {
    return attractionGalleryAssets['Ella Tea Country Hike']!.first;
  }
  if (normalized.contains('galle')) {
    return attractionGalleryAssets['Galle Fort Heritage Walk']!.first;
  }
  if (normalized.contains('kandy')) {
    return attractionGalleryAssets['Kandy Lake and Temple Walk']!.first;
  }
  if (normalized.contains('sinharaja')) {
    return attractionGalleryAssets['Sinharaja Rainforest Nature Walk']!.first;
  }
  if (normalized.contains('udawalawe')) {
    return attractionGalleryAssets['Udawalawe Wildlife Safari']!.first;
  }
  return null;
}
