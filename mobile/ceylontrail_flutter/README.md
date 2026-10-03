# CeylonTrail AI Flutter application

This Flutter application contains the shared authentication foundation for the CeylonTrail AI mobile client.

## Local Android development

The default Android emulator API base URL is:

```text
http://10.0.2.2:5027
```

To override it for another environment, pass a Dart define:

```powershell
flutter run --dart-define=API_BASE_URL=https://api.example.com
```

The login screen calls the ASP.NET Core `/api/auth/login` endpoint. JWT persistence uses `flutter_secure_storage`; the backend remains authoritative for authentication and authorization.

## Itinerary map setup

The itinerary map uses `google_maps_flutter` and only plots latitude/longitude
already returned by the ASP.NET itinerary response. It is disabled by default
until platform credentials are configured:

```powershell
$env:GOOGLE_MAPS_ANDROID_API_KEY = '<Android-restricted-key>'
flutter run --dart-define=CEYLONTRAIL_GOOGLE_MAPS_ENABLED=true
```

For Android, enable **Maps SDK for Android** in Google Cloud and restrict the
key to this app's package and signing certificate. The Gradle build reads the
key from `GOOGLE_MAPS_ANDROID_API_KEY`; no key is stored in the repository.

For Flutter Web, enable **Maps JavaScript API**, replace the placeholder
`YOUR_GOOGLE_MAPS_WEB_API_KEY` in `web/index.html` locally, and restrict the
browser key to the app's allowed localhost/deployed origins. Start web with:

```powershell
flutter run -d chrome --dart-define=CEYLONTRAIL_GOOGLE_MAPS_ENABLED=true
```

If the define is omitted, or coordinates are unavailable, the itinerary
continues to render and shows an unavailable-map state. The existing external
Google Maps links remain available for coordinate-bearing attractions.
