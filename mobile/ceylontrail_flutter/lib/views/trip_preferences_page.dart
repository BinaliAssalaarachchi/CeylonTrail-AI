import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../services/api_client.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';

class TripPreferencesPage extends StatefulWidget {
  const TripPreferencesPage({required this.api, required this.tripId, super.key});
  final TripApiService api;
  final String tripId;
  @override State<TripPreferencesPage> createState() => _TripPreferencesPageState();
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
      setState(() => _error = 'Add a preference type and tell us what matters to you.');
      return;
    }
    setState(() { _saving = true; _error = null; });
    try {
      await widget.api.addPreference(
        tripId: widget.tripId,
        preferenceType: _type.text.trim(),
        value: _value.text.trim(),
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Preference added.')),
      );
      context.pop(true);
    } on ApiException catch (error) {
      if (mounted) setState(() { _error = error.message; _saving = false; });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Manage preferences')),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 620),
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(CeylonSpacing.md, CeylonSpacing.sm, CeylonSpacing.md, 48),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Your travel preferences', style: Theme.of(context).textTheme.headlineMedium),
                const SizedBox(height: 6),
                Text('Add another detail to help CeylonTrail shape the right journey for you.', style: Theme.of(context).textTheme.bodyLarge),
                const SizedBox(height: CeylonSpacing.xl),
                Text('What should we know?', style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: CeylonSpacing.sm),
                TextField(
                  controller: _type,
                  textCapitalization: TextCapitalization.words,
                  decoration: const InputDecoration(
                    labelText: 'Preference type',
                    hintText: 'Travel style, interest, region...',
                  ),
                ),
                const SizedBox(height: CeylonSpacing.md),
                TextField(
                  controller: _value,
                  maxLines: 4,
                  textCapitalization: TextCapitalization.sentences,
                  decoration: const InputDecoration(
                    labelText: 'Your preference',
                    hintText: 'Tell us what you would like to prioritise.',
                  ),
                ),
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(top: CeylonSpacing.md),
                    child: Text(
                      _error!,
                      style: TextStyle(color: Theme.of(context).colorScheme.error),
                    ),
                  ),
                const SizedBox(height: CeylonSpacing.xl),
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton(
                    onPressed: _saving ? null : _add,
                    child: _saving
                        ? const SizedBox(
                            width: 22,
                            height: 22,
                            child: CircularProgressIndicator(color: Colors.white),
                          )
                        : const Text('Save preference'),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
