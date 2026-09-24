import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/attraction_model.dart';
import '../services/attraction_api_service.dart';
import '../services/api_client.dart';
import '../theme/app_theme.dart';
import '../widgets/attraction_card.dart';
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
    _service = AttractionApiService(ApiClient(storage: AuthScope.of(context).storage));
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
    } catch (_) {
      // Category filters remain optional if the categories request fails.
    }
  }

  Future<void> _loadAttractions({bool reset = true}) async {
    if (reset) {
      setState(() { _loading = true; _error = null; _page = 1; });
    } else {
      setState(() => _loadingMore = true);
    }
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

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 730)),
      initialDate: _date ?? DateTime.now(),
    );
    if (picked != null) setState(() => _date = picked);
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
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.toString())));
    }
  }

  @override
  Widget build(BuildContext context) {
    final isTourist = AuthScope.of(context).user?.role == 'Tourist';
    return Scaffold(
      appBar: AppBar(
        title: const Text('Discover'),
        actions: [
          if (isTourist)
            IconButton(
              tooltip: 'Favorites',
              onPressed: () => context.push('/favorites'),
              icon: const Icon(Icons.favorite_border),
            ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _loadAttractions,
        child: CustomScrollView(
          slivers: [
            SliverToBoxAdapter(child: _header()),
            if (_loading)
              const SliverFillRemaining(hasScrollBody: false, child: Center(child: CircularProgressIndicator()))
            else if (_error != null)
              SliverFillRemaining(hasScrollBody: false, child: _errorState())
            else if (_items.isEmpty)
              SliverFillRemaining(hasScrollBody: false, child: _emptyState())
            else ...[
              SliverPadding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
                sliver: SliverList.builder(
                  itemCount: _items.length,
                  itemBuilder: (context, index) => Padding(
                    padding: const EdgeInsets.only(bottom: 14),
                    child: AttractionCard(
                      attraction: _items[index],
                      onFavoriteChanged: isTourist ? () => _toggleFavorite(_items[index]) : null,
                    ),
                  ),
                ),
              ),
              if (_page < _totalPages)
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
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
    );
  }

  Widget _header() => Padding(
    padding: const EdgeInsets.fromLTRB(16, 0, 16, 20),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Places with a story', style: Theme.of(context).textTheme.headlineMedium),
        const SizedBox(height: 6),
        Text('Search real attractions and experiences across Sri Lanka.', style: Theme.of(context).textTheme.bodyMedium),
        const SizedBox(height: 18),
        TextField(
          controller: _searchController,
          textInputAction: TextInputAction.search,
          onSubmitted: (_) => _loadAttractions(),
          decoration: InputDecoration(
            hintText: 'Search attractions',
            prefixIcon: const Icon(Icons.search),
            suffixIcon: IconButton(tooltip: 'Filters', onPressed: () => setState(() => _showFilters = !_showFilters), icon: Icon(_showFilters ? Icons.close : Icons.tune)),
          ),
        ),
        if (_showFilters) _filterPanel(),
      ],
    ),
  );

  Widget _filterPanel() => Card(
    margin: const EdgeInsets.only(top: 12),
    child: Padding(
      padding: const EdgeInsets.all(14),
      child: Column(
        children: [
          DropdownButtonFormField<String>(
            value: _categoryId,
            decoration: const InputDecoration(labelText: 'Category'),
            items: [const DropdownMenuItem(value: null, child: Text('All categories')), ..._categories.map((c) => DropdownMenuItem(value: c.id, child: Text(c.name)))],
            onChanged: (value) => setState(() => _categoryId = value),
          ),
          const SizedBox(height: 10),
          TextField(controller: _districtController, decoration: const InputDecoration(labelText: 'District')),
          const SizedBox(height: 10),
          Row(children: [Expanded(child: TextField(controller: _minPriceController, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Min price'))), const SizedBox(width: 10), Expanded(child: TextField(controller: _maxPriceController, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Max price')))]),
          const SizedBox(height: 10),
          Row(children: [Expanded(child: Text(_date == null ? 'Any availability date' : 'Date: ${_date!.toLocal().toString().split(' ').first}')), TextButton.icon(onPressed: _pickDate, icon: const Icon(Icons.calendar_today_outlined), label: const Text('Date'))]),
          DropdownButtonFormField<String>(value: _sort, decoration: const InputDecoration(labelText: 'Sort'), items: const [DropdownMenuItem(value: 'name_asc', child: Text('Name A–Z')), DropdownMenuItem(value: 'name_desc', child: Text('Name Z–A')), DropdownMenuItem(value: 'price_asc', child: Text('Price low to high')), DropdownMenuItem(value: 'price_desc', child: Text('Price high to low')), DropdownMenuItem(value: 'newest', child: Text('Newest'))], onChanged: (value) => setState(() => _sort = value ?? 'name_asc')),
          const SizedBox(height: 12),
          Row(children: [Expanded(child: OutlinedButton(onPressed: () { setState(() { _categoryId = null; _districtController.clear(); _minPriceController.clear(); _maxPriceController.clear(); _date = null; _sort = 'name_asc'; }); _loadAttractions(); }, child: const Text('Clear'))), const SizedBox(width: 10), Expanded(child: FilledButton(onPressed: _loadAttractions, child: const Text('Apply filters')))]),
        ],
      ),
    ),
  );

  Widget _emptyState() => ListView(children: const [SizedBox(height: 100), Icon(Icons.explore_off_outlined, size: 48), SizedBox(height: 12), Center(child: Text('No attractions match your search.'))]);
  Widget _errorState() => ListView(children: [const SizedBox(height: 100), const Icon(Icons.cloud_off_outlined, size: 48), const SizedBox(height: 12), Center(child: Padding(padding: EdgeInsets.symmetric(horizontal: 24), child: Text('We could not load attractions.'))), const SizedBox(height: 12), Center(child: FilledButton(onPressed: _loadAttractions, child: const Text('Retry')))]);
}
