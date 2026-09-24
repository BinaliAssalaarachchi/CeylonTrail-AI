import 'package:flutter/material.dart';

import '../models/attraction_model.dart';
import '../services/attraction_api_service.dart';
import '../services/api_client.dart';
import '../widgets/attraction_card.dart';
import '../widgets/auth_scope.dart';

class FavoritesPage extends StatefulWidget {
  const FavoritesPage({super.key});
  @override
  State<FavoritesPage> createState() => _FavoritesPageState();
}

class _FavoritesPageState extends State<FavoritesPage> {
  late AttractionApiService _service;
  List<AttractionModel> _items = [];
  bool _loading = true;
  String? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _service = AttractionApiService(ApiClient(storage: AuthScope.of(context).storage));
    if (_loading) _load();
  }

  Future<void> _load() async {
    try {
      final result = await _service.getFavorites();
      if (mounted) setState(() => _items = result.items);
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _remove(AttractionModel attraction) async {
    try { await _service.removeFavorite(attraction.id); await _load(); } catch (error) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.toString()))); }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Favorites')),
    body: _loading
        ? const Center(child: CircularProgressIndicator())
        : _error != null
            ? Center(child: Column(mainAxisSize: MainAxisSize.min, children: [const Text('Unable to load favorites.'), const SizedBox(height: 12), FilledButton(onPressed: () { setState(() { _loading = true; _error = null; }); _load(); }, child: const Text('Retry'))]))
            : _items.isEmpty
                ? RefreshIndicator(onRefresh: _load, child: ListView(children: const [SizedBox(height: 120), Icon(Icons.favorite_border, size: 52), SizedBox(height: 12), Center(child: Text('No favorite attractions yet.'))]))
                : RefreshIndicator(onRefresh: _load, child: ListView.builder(padding: const EdgeInsets.all(16), itemCount: _items.length, itemBuilder: (context, index) => Padding(padding: const EdgeInsets.only(bottom: 14), child: AttractionCard(attraction: _items[index], onFavoriteChanged: () => _remove(_items[index]))))),
  );
}
