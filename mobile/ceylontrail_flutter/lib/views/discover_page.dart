import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/attraction_model.dart';
import '../services/api_client.dart';
import '../services/attraction_api_service.dart';
import '../theme/app_theme.dart';
import '../utils/attraction_images.dart';
import '../widgets/auth_scope.dart';

class DiscoverPage extends StatefulWidget {
  const DiscoverPage({super.key});

  @override
  State<DiscoverPage> createState() => _DiscoverPageState();
}

class _DiscoverPageState extends State<DiscoverPage> {
  late AttractionApiService _service;
  final _searchController = TextEditingController();
  final _districtController = TextEditingController();
  final _minPriceController = TextEditingController();
  final _maxPriceController = TextEditingController();
  List<AttractionModel> _items = [];
  List<CategoryModel> _categories = [];
  String? _categoryId;
  String _sort = 'name_asc';
  DateTime? _date;
  bool _loading = true;
  bool _loadingMore = false;
  bool _showFilters = false;
  String? _error;
  int _page = 1;
  int _totalPages = 1;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _service = AttractionApiService(
      ApiClient(storage: AuthScope.of(context).storage),
    );
    if (_categories.isEmpty) {
      _loadCategories();
      _loadAttractions();
    }
  }

  @override
  void dispose() {
    _searchController.dispose();
    _districtController.dispose();
    _minPriceController.dispose();
    _maxPriceController.dispose();
    super.dispose();
  }

  Future<void> _loadCategories() async {
    try {
      final categories = await _service.getCategories();
      if (mounted) setState(() => _categories = categories);
    } catch (_) {}
  }

  Future<void> _loadAttractions({bool reset = true}) async {
    setState(() {
      if (reset) {
        _loading = true;
        _error = null;
        _page = 1;
      } else {
        _loadingMore = true;
      }
    });
    try {
      final result = await _service.searchAttractions(
        keyword: _searchController.text,
        district: _districtController.text,
        categoryId: _categoryId,
        minPrice: double.tryParse(_minPriceController.text),
        maxPrice: double.tryParse(_maxPriceController.text),
        date: _date,
        sort: _sort,
        page: reset ? 1 : _page + 1,
        pageSize: 12,
      );
      if (!mounted) return;
      setState(() {
        _items = reset ? result.items : [..._items, ...result.items];
        _page = result.page;
        _totalPages = result.totalPages;
        _error = null;
      });
    } catch (error) {
      if (mounted && reset) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() { _loading = false; _loadingMore = false; });
    }
  }

  Future<void> _toggleFavorite(AttractionModel attraction) async {
    try {
      if (attraction.isFavorite) {
        await _service.removeFavorite(attraction.id);
      } else {
        await _service.addFavorite(attraction.id);
      }
      await _loadAttractions();
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.toString())));
      }
    }
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 730)),
      initialDate: _date ?? DateTime.now(),
    );
    if (picked != null) setState(() => _date = picked);
  }

  @override
  Widget build(BuildContext context) {
    final isTourist = AuthScope.of(context).user?.role == 'Tourist';
    return Scaffold(
      backgroundColor: CeylonColors.canvas,
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 760),
            child: RefreshIndicator(
              onRefresh: _loadAttractions,
              child: CustomScrollView(
                slivers: [
                  SliverToBoxAdapter(child: _header()),
                  if (_loading)
                    const SliverFillRemaining(
                      hasScrollBody: false,
                      child: Center(child: CircularProgressIndicator()),
                    )
                  else if (_error != null)
                    SliverFillRemaining(hasScrollBody: false, child: _errorState())
                  else if (_items.isEmpty)
                    SliverFillRemaining(hasScrollBody: false, child: _emptyState())
                  else ...[
                    SliverPadding(
                      padding: const EdgeInsets.fromLTRB(20, 8, 20, 20),
                      sliver: SliverGrid.builder(
                        gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: 2,
                          crossAxisSpacing: 12,
                          mainAxisSpacing: 14,
                          childAspectRatio: .72,
                        ),
                        itemCount: _items.length,
                        itemBuilder: (context, index) => _DiscoverCard(
                          attraction: _items[index],
                          isTourist: isTourist,
                          onFavorite: () => _toggleFavorite(_items[index]),
                        ),
                      ),
                    ),
                    if (_page < _totalPages)
                      SliverToBoxAdapter(
                        child: Padding(
                          padding: const EdgeInsets.fromLTRB(20, 0, 20, 28),
                          child: OutlinedButton(
                            onPressed: _loadingMore ? null : () => _loadAttractions(reset: false),
                            child: Text(_loadingMore ? 'Loading…' : 'Load more'),
                          ),
                        ),
                      ),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _header() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('DISCOVER SRI LANKA', style: Theme.of(context).textTheme.labelSmall?.copyWith(letterSpacing: 1.6, fontWeight: FontWeight.w800, color: CeylonColors.tea)),
              if (AuthScope.of(context).user?.role == 'Tourist')
                IconButton(onPressed: () => context.push('/favorites'), icon: const Icon(Icons.favorite_border_rounded), tooltip: 'Favorites'),
            ],
          ),
          const SizedBox(height: 5),
          Text('Find somewhere unforgettable.', style: Theme.of(context).textTheme.displaySmall?.copyWith(fontSize: 30)),
          const SizedBox(height: 6),
          Text('Beaches, heritage, wildlife and highland escapes.', style: Theme.of(context).textTheme.bodyMedium),
          const SizedBox(height: 18),
          TextField(
            controller: _searchController,
            textInputAction: TextInputAction.search,
            onSubmitted: (_) => _loadAttractions(),
            decoration: InputDecoration(hintText: 'Search destinations and experiences', prefixIcon: const Icon(Icons.search_rounded), suffixIcon: IconButton(onPressed: () => setState(() => _showFilters = !_showFilters), icon: Icon(_showFilters ? Icons.close : Icons.tune_rounded))),
          ),
          const SizedBox(height: 12),
          _categoryChips(),
          if (_showFilters) _filterPanel(),
        ],
      ),
    );
  }

  Widget _categoryChips() {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          _chip('All', null),
          ..._categories.map((category) => _chip(category.name, category.id)),
        ],
      ),
    );
  }

  Widget _chip(String label, String? categoryId) {
    final selected = _categoryId == categoryId;
    return Padding(
      padding: const EdgeInsets.only(right: 8),
      child: ActionChip(
        onPressed: () { setState(() => _categoryId = categoryId); _loadAttractions(); },
        label: Text(label),
        side: BorderSide.none,
        backgroundColor: selected ? CeylonColors.forest : CeylonColors.ivory,
        labelStyle: TextStyle(color: selected ? Colors.white : CeylonColors.forest, fontSize: 12, fontWeight: FontWeight.w700),
      ),
    );
  }

  Widget _filterPanel() {
    return Padding(
      padding: const EdgeInsets.only(top: 12),
      child: Column(
        children: [
          Row(children: [Expanded(child: TextField(controller: _districtController, decoration: const InputDecoration(labelText: 'District'))), const SizedBox(width: 10), Expanded(child: TextField(controller: _minPriceController, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Min price'))), const SizedBox(width: 10), Expanded(child: TextField(controller: _maxPriceController, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Max price')))]),
          const SizedBox(height: 10),
          Row(children: [Expanded(child: Text(_date == null ? 'Any availability date' : 'Date: ${_date!.toLocal().toString().split(' ').first}')), TextButton.icon(onPressed: _pickDate, icon: const Icon(Icons.calendar_today_outlined), label: const Text('Date'))]),
          Row(children: [Expanded(child: OutlinedButton(onPressed: () { setState(() { _categoryId = null; _districtController.clear(); _minPriceController.clear(); _maxPriceController.clear(); _date = null; _sort = 'name_asc'; }); _loadAttractions(); }, child: const Text('Clear'))), const SizedBox(width: 10), Expanded(child: FilledButton(onPressed: _loadAttractions, child: const Text('Apply filters')))]),
        ],
      ),
    );
  }

  Widget _emptyState() => ListView(children: const [SizedBox(height: 120), Icon(Icons.explore_off_outlined, size: 48, color: CeylonColors.tea), SizedBox(height: 12), Center(child: Text('No attractions match your search.'))]);
  Widget _errorState() => ListView(children: [const SizedBox(height: 120), const Icon(Icons.cloud_off_outlined, size: 48), const SizedBox(height: 12), const Center(child: Text('We could not load attractions.')), const SizedBox(height: 12), Center(child: FilledButton(onPressed: _loadAttractions, child: const Text('Retry')))]);
}

class _DiscoverCard extends StatelessWidget {
  const _DiscoverCard({required this.attraction, required this.isTourist, required this.onFavorite});

  final AttractionModel attraction;
  final bool isTourist;
  final VoidCallback onFavorite;

  @override
  Widget build(BuildContext context) {
    final gallery = attractionGallery(attraction);
    final image = gallery.isEmpty ? null : gallery.first;
    return Material(
      color: CeylonColors.forestDeep,
      borderRadius: BorderRadius.circular(24),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push('/discover/${attraction.id}'),
        child: Stack(
          fit: StackFit.expand,
          children: [
            image == null
                ? _PhotoUnavailable(attraction: attraction)
                : image.startsWith('http')
                    ? Image.network(image, fit: BoxFit.cover, errorBuilder: (_, __, ___) => _PhotoUnavailable(attraction: attraction))
                    : Image.asset(image, fit: BoxFit.cover, errorBuilder: (_, __, ___) => _PhotoUnavailable(attraction: attraction)),
            const DecoratedBox(decoration: BoxDecoration(gradient: LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Colors.transparent, Color(0xE600241A)]))),
            if (isTourist) Positioned(top: 10, right: 10, child: Material(color: Colors.white.withValues(alpha: .9), shape: const CircleBorder(), child: InkWell(onTap: onFavorite, customBorder: const CircleBorder(), child: Padding(padding: const EdgeInsets.all(8), child: Icon(attraction.isFavorite ? Icons.favorite : Icons.favorite_border, size: 18, color: attraction.isFavorite ? CeylonColors.error : CeylonColors.forest))))),
            Positioned(left: 14, right: 12, bottom: 14, child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(attraction.name, maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 17)), const SizedBox(height: 4), Text(attraction.district, style: const TextStyle(color: Colors.white70, fontSize: 12)), const SizedBox(height: 4), Text(attraction.category?.name ?? 'Experience', maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(color: CeylonColors.mint, fontSize: 10, fontWeight: FontWeight.w800, letterSpacing: .5))])),
          ],
        ),
      ),
    );
  }
}

class _PhotoUnavailable extends StatelessWidget {
  const _PhotoUnavailable({required this.attraction});

  final AttractionModel attraction;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: CeylonColors.forest,
      child: Center(
        child: Padding(
          padding: const EdgeInsets.all(18),
          child: Text(
            attraction.district,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: Colors.white70,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
      ),
    );
  }
}
