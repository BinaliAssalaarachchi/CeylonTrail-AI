import 'package:flutter/foundation.dart';

class ApiConfig {
  const ApiConfig._();

  static String get baseUrl {
    const fromEnv = String.fromEnvironment('API_BASE_URL');
    if (fromEnv.isNotEmpty) {
      return fromEnv;
    }
    if (kIsWeb) {
      final host = Uri.base.host.isNotEmpty ? Uri.base.host : 'localhost';
      return 'http://$host:5027';
    }
    return 'http://10.0.2.2:5027';
  }

  static String resolveImageUrl(String? rawUrl) {
    if (rawUrl == null || rawUrl.trim().isEmpty) return '';
    var url = rawUrl.trim();
    if (url.startsWith('data:') || url.startsWith('blob:')) return url;
    if (url.contains('/api/images/proxy')) return url;

    if (!kIsWeb && (url.startsWith('http://localhost') || url.startsWith('http://127.0.0.1') || url.startsWith('https://localhost') || url.startsWith('https://127.0.0.1'))) {
      final uri = Uri.tryParse(url);
      final baseUri = Uri.tryParse(baseUrl);
      if (uri != null && baseUri != null) {
        url = uri.replace(host: baseUri.host, port: baseUri.port, scheme: baseUri.scheme).toString();
      }
    }

    if (url.startsWith('http://') || url.startsWith('https://')) {
      return url;
    }
    if (url.startsWith('/')) {
      return '$baseUrl$url';
    }
    if (url.startsWith('uploads/')) {
      return '$baseUrl/$url';
    }
    return url;
  }
}
