import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/trip_model.dart';
import '../services/api_client.dart';
import '../services/trip_api_service.dart';
import '../theme/app_theme.dart';
import 'trip_widgets.dart';

class TripFormPage extends StatefulWidget {
  const TripFormPage({
    required this.api,
    this.trip,
    this.initialStartDate,
    this.initialEndDate,
    super.key,
  });
  final TripApiService api;
  final Trip? trip;
  final DateTime? initialStartDate;
  final DateTime? initialEndDate;
  bool get isEditing => trip != null;

  @override
  State<TripFormPage> createState() => _TripFormPageState();
}

class _TripFormPageState extends State<TripFormPage> {
  late final TextEditingController _name;
  late final TextEditingController _budget;
  late final TextEditingController _objective;
  late DateTime? _start;
  late DateTime? _end;
  String? _status;
  String? _createdTripId;
  var _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    final trip = widget.trip;
    _name = TextEditingController(text: trip?.name);
    _budget = TextEditingController(
      text: trip == null ? '' : trip.budget.toStringAsFixed(2),
    );
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
    final picked = await showDatePicker(
      context: context,
      firstDate: DateTime(2020),
      lastDate: DateTime(2100),
      initialDate: (start ? _start : _end) ?? DateTime.now(),
    );
    if (picked != null) {
      setState(() {
        if (start) {
          _start = picked;
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
    if (name.isEmpty ||
        _start == null ||
        _end == null ||
        budget == null ||
        budget < 0) {
      setState(
        () => _error = 'Enter a name, valid dates, and a non-negative budget.',
      );
      return;
    }
    if (!widget.isEditing && objective.isEmpty) {
      setState(() => _error = 'Enter a trip objective.');
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
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(const SnackBar(content: Text('Trip updated.')));
        context.pop(result);
        return;
      }

      final tripId =
          _createdTripId ??
          (await widget.api.createTrip(
            name: name,
            startDate: _start!,
            endDate: _end!,
            budget: budget,
          )).id;
      _createdTripId ??= tripId;
      await widget.api.addPreference(
        tripId: tripId,
        preferenceType: 'Objective',
        value: objective,
      );
      if (!mounted) return;
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Trip created.')));
      context.go('/trips/$tripId');
    } on ApiException catch (error) {
      if (mounted) {
        setState(() {
          _error = _createdTripId == null
              ? error.message
              : 'Trip created, but the objective could not be saved. Please try again. ${error.message}';
          _saving = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(widget.isEditing ? 'Edit trip' : 'Create trip')),
    body: SingleChildScrollView(
      padding: const EdgeInsets.all(CeylonSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _name,
            textCapitalization: TextCapitalization.sentences,
            decoration: const InputDecoration(labelText: 'Trip name'),
          ),
          if (!widget.isEditing) ...[
            const SizedBox(height: CeylonSpacing.md),
            TextField(
              controller: _objective,
              maxLines: 2,
              textCapitalization: TextCapitalization.sentences,
              decoration: const InputDecoration(labelText: 'Trip Objective'),
            ),
          ],
          const SizedBox(height: CeylonSpacing.md),
          _dateButton('Start date', _start, () => _pickDate(true)),
          const SizedBox(height: CeylonSpacing.sm),
          _dateButton('End date', _end, () => _pickDate(false)),
          const SizedBox(height: CeylonSpacing.md),
          TextField(
            controller: _budget,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            decoration: const InputDecoration(
              labelText: 'Budget (LKR)',
              prefixText: 'LKR ',
            ),
          ),
          if (widget.isEditing) ...[
            const SizedBox(height: CeylonSpacing.md),
            DropdownButtonFormField<String>(
              initialValue: _status,
              decoration: const InputDecoration(labelText: 'Status'),
              items: _allowedStatuses()
                  .map(
                    (value) =>
                        DropdownMenuItem(value: value, child: Text(value)),
                  )
                  .toList(),
              onChanged: _saving
                  ? null
                  : (value) => setState(() => _status = value),
            ),
          ],
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
            onPressed: _saving ? null : _save,
            child: _saving
                ? const SizedBox(
                    height: 22,
                    width: 22,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Colors.white,
                    ),
                  )
                : Text(widget.isEditing ? 'Save changes' : 'Create trip'),
          ),
        ],
      ),
    ),
  );

  Widget _dateButton(String label, DateTime? value, VoidCallback onPressed) =>
      OutlinedButton.icon(
        onPressed: _saving ? null : onPressed,
        icon: const Icon(Icons.calendar_today_outlined),
        label: Align(
          alignment: Alignment.centerLeft,
          child: Text(value == null ? label : '$label: ${displayDate(value)}'),
        ),
      );
}
