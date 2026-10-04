import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../services/auth_service.dart';
import '../theme/app_theme.dart';
import '../widgets/auth_scope.dart';
import '../widgets/auth_visual_shell.dart';

class RegisterPage extends StatefulWidget {
  const RegisterPage({super.key});

  @override
  State<RegisterPage> createState() => _RegisterPageState();
}

class _RegisterPageState extends State<RegisterPage> {
  final _formKey = GlobalKey<FormState>();
  final _firstNameController = TextEditingController();
  final _lastNameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  bool _isSubmitting = false;
  bool _obscurePassword = true;
  String? _error;

  AuthService get _authService => AuthScope.of(context);

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  String? _required(String? value, String label) => value == null || value.trim().isEmpty ? 'Enter your $label.' : null;

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() { _isSubmitting = true; _error = null; });
    final succeeded = await _authService.register(firstName: _firstNameController.text, lastName: _lastNameController.text, email: _emailController.text, password: _passwordController.text);
    if (!mounted) return;
    setState(() { _isSubmitting = false; _error = succeeded ? null : _authService.error; });
    if (succeeded) context.go('/');
  }

  @override
  Widget build(BuildContext context) => AuthVisualShell(
    title: 'Start your journey',
    subtitle: 'Create your traveller account.',
    form: Form(
      key: _formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(child: TextFormField(controller: _firstNameController, textCapitalization: TextCapitalization.words, decoration: const InputDecoration(labelText: 'First name'), validator: (value) => _required(value, 'first name'))),
              const SizedBox(width: 10),
              Expanded(child: TextFormField(controller: _lastNameController, textCapitalization: TextCapitalization.words, decoration: const InputDecoration(labelText: 'Last name'), validator: (value) => _required(value, 'last name'))),
            ],
          ),
          const SizedBox(height: 12),
          TextFormField(controller: _emailController, keyboardType: TextInputType.emailAddress, autofillHints: const [AutofillHints.email], decoration: const InputDecoration(labelText: 'Email'), validator: (value) { if (_required(value, 'email') != null) return _required(value, 'email'); return RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(value!.trim()) ? null : 'Enter a valid email address.'; }),
          const SizedBox(height: 12),
          TextFormField(
            controller: _passwordController,
            obscureText: _obscurePassword,
            autofillHints: const [AutofillHints.newPassword],
            decoration: InputDecoration(labelText: 'Password', suffixIcon: IconButton(tooltip: _obscurePassword ? 'Show password' : 'Hide password', onPressed: () => setState(() => _obscurePassword = !_obscurePassword), icon: Icon(_obscurePassword ? Icons.visibility_outlined : Icons.visibility_off_outlined))),
            validator: (value) => value == null || value.length < 8 ? 'Use at least 8 characters.' : null,
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: _confirmPasswordController,
            obscureText: _obscurePassword,
            autofillHints: const [AutofillHints.newPassword],
            decoration: const InputDecoration(labelText: 'Confirm password'),
            validator: (value) => value != _passwordController.text ? 'Passwords do not match.' : null,
          ),
          if (_error != null) ...[const SizedBox(height: 12), Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error))],
          const SizedBox(height: 22),
          SizedBox(height: 54, child: ElevatedButton(onPressed: _isSubmitting ? null : _submit, child: _isSubmitting ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white)) : const Text('Create account'))),
        ],
      ),
    ),
    footer: Wrap(alignment: WrapAlignment.center, crossAxisAlignment: WrapCrossAlignment.center, children: [const Text('Already have an account?', style: TextStyle(color: CeylonColors.inkMuted)), TextButton(onPressed: _isSubmitting ? null : () => context.go('/login'), child: const Text('Sign in'))]),
  );
}
