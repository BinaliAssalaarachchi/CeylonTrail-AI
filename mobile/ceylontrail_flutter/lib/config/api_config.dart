import 'package:flutter/foundation.dart';

class ApiConfig {
  const ApiConfig._();

  static const baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: kIsWeb
        ? 'http://localhost:5027'
        : 'http://10.0.2.2:5027',
  );
}
