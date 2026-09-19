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
