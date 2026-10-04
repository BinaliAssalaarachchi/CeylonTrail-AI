import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../models/trip_model.dart';
import '../theme/app_theme.dart';

/// Enable only when the platform-specific Google Maps credentials are configured.
const itineraryMapConfigured = bool.fromEnvironment(
  'CEYLONTRAIL_GOOGLE_MAPS_ENABLED',
  defaultValue: false,
);

class ItineraryMapPanel extends StatefulWidget {
  const ItineraryMapPanel({required this.items, super.key});
  final List<ItineraryItem> items;

  @override
  State<ItineraryMapPanel> createState() => _ItineraryMapPanelState();
}

class _ItineraryMapPanelState extends State<ItineraryMapPanel> {
  GoogleMapController? _controller;

  List<ItineraryItem> get _locatedItems => widget.items
      .where((item) => item.latitude != null && item.longitude != null)
      .toList();

  @override
  Widget build(BuildContext context) {
    final items = _locatedItems;
    if (items.isEmpty) {
      return const _MapUnavailable(
        message: 'Map locations are not available for this itinerary yet.',
      );
    }
    if (!itineraryMapConfigured) {
      return const _MapUnavailable(
        message: 'Interactive maps are not configured on this device yet.',
      );
    }

    final first = items.first;
    return GoogleMap(
      initialCameraPosition: CameraPosition(
        target: LatLng(first.latitude!, first.longitude!),
        zoom: items.length == 1 ? 14 : 10,
      ),
      markers: {
        for (final item in items)
          Marker(
            markerId: MarkerId(item.id),
            position: LatLng(item.latitude!, item.longitude!),
            infoWindow: InfoWindow(
              title: item.attractionName ?? 'Planned experience',
              snippet: [item.address, item.district]
                  .whereType<String>()
                  .where((value) => value.trim().isNotEmpty)
                  .join(', '),
            ),
          ),
      },
      myLocationButtonEnabled: false,
      zoomControlsEnabled: true,
      onMapCreated: (controller) {
        _controller = controller;
        if (items.length > 1) _fitMarkers(items);
      },
    );
  }

  Future<void> _fitMarkers(List<ItineraryItem> items) async {
    final controller = _controller;
    if (controller == null || items.length < 2) return;
    var minLat = items.first.latitude!;
    var maxLat = minLat;
    var minLng = items.first.longitude!;
    var maxLng = minLng;
    for (final item in items.skip(1)) {
      final lat = item.latitude!;
      final lng = item.longitude!;
      minLat = lat < minLat ? lat : minLat;
      maxLat = lat > maxLat ? lat : maxLat;
      minLng = lng < minLng ? lng : minLng;
      maxLng = lng > maxLng ? lng : maxLng;
    }
    await controller.animateCamera(
      CameraUpdate.newLatLngBounds(
        LatLngBounds(
          southwest: LatLng(minLat, minLng),
          northeast: LatLng(maxLat, maxLng),
        ),
        48,
      ),
    );
  }
}

class _MapUnavailable extends StatelessWidget {
  const _MapUnavailable({required this.message});
  final String message;

  @override
  Widget build(BuildContext context) => Container(
    color: CeylonColors.surfaceSoft,
    alignment: Alignment.center,
    padding: const EdgeInsets.all(CeylonSpacing.lg),
    child: Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(Icons.map_outlined, color: CeylonColors.inkMuted, size: 34),
        const SizedBox(height: CeylonSpacing.sm),
        Text(message, textAlign: TextAlign.center),
      ],
    ),
  );
}
