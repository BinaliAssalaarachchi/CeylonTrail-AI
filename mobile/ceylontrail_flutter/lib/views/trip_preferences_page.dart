import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../services/api_client.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';

class TripPreferencesPage extends StatefulWidget {
  const TripPreferencesPage({required this.api, required this.tripId, super.key});

  final TripApiService api;
  final String tripId;

  @override
  State<TripPreferencesPage> createState() => _TripPreferencesPageState();
}

class _TripPreferencesPageState extends State<TripPreferencesPage> {
  final _type = TextEditingController();
  final _value = TextEditingController();
  var _saving = false;
  String? _error;

  @override
  void dispose() {
    _type.dispose();
    _value.dispose();
    super.dispose();
  }

  Future<void> _add() async {
    if (_type.text.trim().isEmpty || _value.text.trim().isEmpty) {
      setState(() => _error = 'Both preference type and value are required.');
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await widget.api.addPreference(
        tripId: widget.tripId,
        preferenceType: _type.text,
        value: _value.text,
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Preference added.')),
      );
      context.pop(true);
    } on ApiException catch (error) {
      if (mounted) {
        setState(() {
          _error = error.message;
          _saving = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Trip preferences')),
    body: Padding(
      padding: const EdgeInsets.all(CeylonSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _type,
            decoration: const InputDecoration(
              labelText: 'Preference type',
              hintText: 'Interest, Region, TravelStyle',
            ),
          ),
          const SizedBox(height: CeylonSpacing.md),
          TextField(
            controller: _value,
            decoration: const InputDecoration(labelText: 'Value'),
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(top: CeylonSpacing.md),
              child: Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          const SizedBox(height: CeylonSpacing.lg),
          ElevatedButton(
            onPressed: _saving ? null : _add,
            child: _saving
                ? const CircularProgressIndicator(color: Colors.white)
                : const Text('Add preference'),
          ),
        ],
      ),
    ),
  );
}
