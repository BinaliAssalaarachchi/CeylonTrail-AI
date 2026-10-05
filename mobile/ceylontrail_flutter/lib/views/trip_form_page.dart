import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/trip_model.dart';
import '../services/api_client.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';
import 'trip_widgets.dart';

class TripFormPage extends StatefulWidget {
  const TripFormPage({required this.api, this.trip, this.initialStartDate, this.initialEndDate, super.key});
  final TripApiService api;
  final Trip? trip;
  final DateTime? initialStartDate;
  final DateTime? initialEndDate;
  bool get isEditing => trip != null;
  @override State<TripFormPage> createState() => _TripFormPageState();
}

class _TripFormPageState extends State<TripFormPage> {
  late final TextEditingController _name;
  late final TextEditingController _budget;
  late final TextEditingController _objective;
  late DateTime? _start;
  late DateTime? _end;
  String? _status;
  String? _createdTripId;
  String? _error;
  var _saving = false;

  @override
  void initState() {
    super.initState();
    final trip = widget.trip;
    _name = TextEditingController(text: trip?.name);
    _budget = TextEditingController(text: trip == null ? '' : trip.budget.toStringAsFixed(2));
    _objective = TextEditingController();
    _start = trip?.startDate ?? widget.initialStartDate;
    _end = trip?.endDate ?? widget.initialEndDate;
    _status = trip?.status;
  }

  @override
  void dispose() {
    _name.dispose();
    _budget.dispose();
    _objective.dispose();
    super.dispose();
  }

  Future<void> _pickDate(bool start) async {
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final minDate = widget.isEditing ? DateTime(2020) : today;
    final initial = (start ? _start : _end) ?? today;
    final effectiveInitial = initial.isBefore(minDate) ? minDate : initial;

    final picked = await showDatePicker(
      context: context,
      firstDate: minDate,
      lastDate: DateTime(2100),
      initialDate: effectiveInitial,
    );
    if (picked != null) {
      setState(() {
        if (start) {
          _start = picked;
          if (_end != null && _end!.isBefore(picked)) {
            _end = picked;
          }
        } else {
          _end = picked;
        }
      });
    }
  }

  List<String> _allowedStatuses() {
    switch (_status) {
      case 'Draft':
        return ['Draft', 'Planned', 'Cancelled'];
      case 'Planned':
        return ['Planned', 'Completed', 'Cancelled'];
      default:
        return [_status ?? 'Draft'];
    }
  }

  Future<void> _save() async {
    final name = _name.text.trim();
    final budget = double.tryParse(_budget.text.trim());
    final objective = _objective.text.trim();
    setState(() => _error = null);
    if (name.isEmpty || _start == null || _end == null || budget == null || budget < 0) {
      setState(() => _error = 'Enter a name, valid dates, and a non-negative budget.');
      return;
    }
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    if (!widget.isEditing && _start!.isBefore(today)) {
      setState(() => _error = 'Start date cannot be a past date.');
      return;
    }
    if (!widget.isEditing && objective.isEmpty) {
      setState(() => _error = 'Tell us what you would like this journey to include.');
      return;
    }
    if (_end!.isBefore(_start!)) {
      setState(() => _error = 'End date must be on or after the start date.');
      return;
    }
    setState(() => _saving = true);
    try {
      if (widget.isEditing) {
        final result = await widget.api.updateTrip(
          id: widget.trip!.id,
          name: name,
          startDate: _start!,
          endDate: _end!,
          budget: budget,
          status: _status == widget.trip!.status ? null : _status,
        );
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Journey updated.')));
        context.pop(result);
        return;
      }
      final tripId = _createdTripId ?? (await widget.api.createTrip(
        name: name,
        startDate: _start!,
        endDate: _end!,
        budget: budget,
      )).id;
      _createdTripId ??= tripId;
      await widget.api.addPreference(tripId: tripId, preferenceType: 'Objective', value: objective);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Journey created.')));
      context.go('/trips/' + tripId);
    } on ApiException catch (error) {
      if (mounted) {
        setState(() {
          _error = _createdTripId == null
              ? error.message
              : 'Journey created, but your preferences could not be saved. Please try again. ' + error.message;
          _saving = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(widget.isEditing ? 'Edit journey' : 'Plan a new journey')),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 620),
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(CeylonSpacing.md, CeylonSpacing.sm, CeylonSpacing.md, 48),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(widget.isEditing ? 'Shape your journey' : 'Plan a new journey', style: Theme.of(context).textTheme.headlineMedium),
                const SizedBox(height: 6),
                Text(widget.isEditing ? 'Update the details that guide your itinerary.' : 'Tell us the basics. You can shape the details as you go.', style: Theme.of(context).textTheme.bodyLarge),
                const SizedBox(height: CeylonSpacing.xl),
                const _FieldLabel(title: 'Trip name', hint: 'Give your journey a name'),
                TextField(controller: _name, textCapitalization: TextCapitalization.sentences, decoration: const InputDecoration(hintText: 'e.g. A week in the hill country')),
                if (!widget.isEditing) ...[
                  const SizedBox(height: CeylonSpacing.lg),
                  const _FieldLabel(title: 'Objective', hint: 'What kind of trip are you imagining?'),
                  const Text('Tell CeylonTrail what you would like to see, do or prioritise.'),
                  const SizedBox(height: 8),
                  TextField(controller: _objective, maxLines: 4, textCapitalization: TextCapitalization.sentences, decoration: const InputDecoration(hintText: 'I would love a relaxed journey with...')),
                ],
                const SizedBox(height: CeylonSpacing.lg),
                const _FieldLabel(title: 'When are you travelling?', hint: 'Choose your journey dates'),
                LayoutBuilder(
                  builder: (context, constraints) {
                    final start = _dateButton('Start date', _start, () => _pickDate(true));
                    final end = _dateButton('End date', _end, () => _pickDate(false));
                    return constraints.maxWidth >= 460
                        ? Row(children: [Expanded(child: start), const SizedBox(width: CeylonSpacing.sm), Expanded(child: end)])
                        : Column(children: [start, const SizedBox(height: CeylonSpacing.sm), end]);
                  },
                ),
                const SizedBox(height: CeylonSpacing.lg),
                const _FieldLabel(title: "What's your approximate budget?", hint: 'Keep LKR clearly indicated'),
                TextField(controller: _budget, keyboardType: const TextInputType.numberWithOptions(decimal: true), decoration: const InputDecoration(prefixText: 'LKR ', hintText: '60,000')),
                if (widget.isEditing) ...[
                  const SizedBox(height: CeylonSpacing.lg),
                  const _FieldLabel(title: 'Journey state', hint: 'Keep the current state accurate'),
                  DropdownButtonFormField<String>(
                    initialValue: _status,
                    decoration: const InputDecoration(),
                    items: _allowedStatuses().map((value) => DropdownMenuItem(value: value, child: Text(friendlyStatus(value)))).toList(),
                    onChanged: _saving ? null : (value) => setState(() => _status = value),
                  ),
                ],
                if (_error != null) Padding(padding: const EdgeInsets.only(top: CeylonSpacing.md), child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
                const SizedBox(height: CeylonSpacing.xl),
                SizedBox(width: double.infinity, child: ElevatedButton(onPressed: _saving ? null : _save, child: _saving ? const SizedBox(height: 22, width: 22, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white)) : Text(widget.isEditing ? 'Save changes' : 'Create my trip'))),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _dateButton(String label, DateTime? value, VoidCallback onPressed) {
    return OutlinedButton.icon(
      onPressed: _saving ? null : onPressed,
      icon: const Icon(Icons.calendar_today_outlined),
      label: Align(alignment: Alignment.centerLeft, child: Text(value == null ? label : label + ': ' + displayDate(value))),
    );
  }
}

class _FieldLabel extends StatelessWidget {
  const _FieldLabel({required this.title, required this.hint});
  final String title;
  final String hint;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text(title, style: Theme.of(context).textTheme.titleMedium),
      Text(hint, style: const TextStyle(color: CeylonColors.inkMuted)),
    ]),
  );
}
