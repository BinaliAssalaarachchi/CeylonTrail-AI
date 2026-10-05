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

List<String> attractionGallery(AttractionModel attraction) {
  if (attraction.images.isNotEmpty) {
    return attraction.images.map((img) => img.imageUrl).toList();
  }
  if (attractionGalleryAssets.containsKey(attraction.name)) {
    return attractionGalleryAssets[attraction.name]!;
  }
  final hero = destinationHeroAsset(attraction.name) ?? destinationHeroAsset(attraction.district);
  if (hero != null) {
    return [hero];
  }
  return const [];
}

String? destinationHeroAsset(String destination) {
  final normalized = destination.trim().toLowerCase();
  if (normalized.contains('sigiriya') || normalized.contains('pidurangala') || normalized.contains('dambulla') || normalized.contains('anuradhapura') || normalized.contains('polonnaruwa') || normalized.contains('matale')) {
    return attractionGalleryAssets['Sigiriya Heritage Sunrise Trail']!.first;
  }
  if (normalized.contains('arugam bay') || normalized.contains('ampara') || normalized.contains('pottuvil') || normalized.contains('surf')) {
    return attractionGalleryAssets['Arugam Bay Coastal Surf Lesson']!.first;
  }
  if (normalized.contains('bentota') || normalized.contains('trincomalee') || normalized.contains('pigeon') || normalized.contains('madu')) {
    return attractionGalleryAssets['Bentota River Kayak']!.first;
  }
  if (normalized.contains('ella') || normalized.contains('badulla') || normalized.contains('tea country')) {
    return attractionGalleryAssets['Ella Tea Country Hike']!.first;
  }
  if (normalized.contains('galle') || normalized.contains('fort')) {
    return attractionGalleryAssets['Galle Fort Heritage Walk']!.first;
  }
  if (normalized.contains('kandy') || normalized.contains('jaffna') || normalized.contains('nallur') || normalized.contains('temple') || normalized.contains('relic')) {
    return attractionGalleryAssets['Kandy Lake and Temple Walk']!.first;
  }
  if (normalized.contains('sinharaja') || normalized.contains('kitulgala') || normalized.contains('kegalle') || normalized.contains('rainforest')) {
    return attractionGalleryAssets['Sinharaja Rainforest Nature Walk']!.first;
  }
  if (normalized.contains('udawalawe') || normalized.contains('minneriya') || normalized.contains('elephant')) {
    return attractionGalleryAssets['Udawalawe Wildlife Safari']!.first;
  }
  if (normalized.contains('yala') || normalized.contains('hambantota') || normalized.contains('safari') || normalized.contains('leopard')) {
    return 'assets/images/yala-wildlife.jpg';
  }
  if (normalized.contains('nuwara eliya') || normalized.contains('horton') || normalized.contains('pedro')) {
    return 'assets/images/tea-country-hero.jpg';
  }
  if (normalized.contains('mirissa') || normalized.contains('matara') || normalized.contains('whale')) {
    return 'assets/images/mirissa-coast.jpg';
  }
  return 'assets/images/sigiriya-hero.png';
}
