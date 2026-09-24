import 'package:flutter/widgets.dart';

import 'app.dart';
import 'services/api_client.dart';
import 'services/auth_api_service.dart';
import 'services/auth_service.dart';
import 'services/auth_storage.dart';
import 'services/trip_api_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final storage = SecureAuthStorage();
  final apiClient = ApiClient(storage: storage);
  final authService = AuthService(
    api: AuthApiService(apiClient),
    storage: storage,
  );
  await authService.restore();

  runApp(CeylonTrailApp(authService: authService, tripApiService: TripApiService(apiClient)));
}
