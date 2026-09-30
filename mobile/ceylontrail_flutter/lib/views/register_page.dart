import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../services/auth_service.dart';
import '../theme/app_theme.dart';
import '../widgets/auth_scope.dart';
import '../widgets/brand_mark.dart';

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
  bool _isSubmitting = false;
  String? _error;

  AuthService get _authService => AuthScope.of(context);

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _isSubmitting = true;
      _error = null;
    });
    final succeeded = await _authService.register(
      firstName: _firstNameController.text,
      lastName: _lastNameController.text,
      email: _emailController.text,
      password: _passwordController.text,
    );
    if (!mounted) return;
    setState(() {
      _isSubmitting = false;
      _error = succeeded ? null : _authService.error;
    });
    if (succeeded) context.go('/');
  }

  String? _required(String? value, String label) =>
      value == null || value.trim().isEmpty ? 'Enter your $label.' : null;

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: CeylonColors.ivory,
    body: SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(CeylonSpacing.lg),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 520),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const BrandLockup(),
              const SizedBox(height: CeylonSpacing.xl),
              Text('Create your traveller account', style: Theme.of(context).textTheme.displaySmall),
              const SizedBox(height: CeylonSpacing.sm),
              Text('Start planning thoughtful journeys across Sri Lanka.', style: Theme.of(context).textTheme.bodyLarge),
              const SizedBox(height: CeylonSpacing.lg),
              Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextFormField(controller: _firstNameController, textCapitalization: TextCapitalization.words, decoration: const InputDecoration(labelText: 'First name'), validator: (value) => _required(value, 'your first name')),
                    const SizedBox(height: CeylonSpacing.md),
                    TextFormField(controller: _lastNameController, textCapitalization: TextCapitalization.words, decoration: const InputDecoration(labelText: 'Last name'), validator: (value) => _required(value, 'your last name')),
                    const SizedBox(height: CeylonSpacing.md),
                    TextFormField(controller: _emailController, keyboardType: TextInputType.emailAddress, decoration: const InputDecoration(labelText: 'Email'), validator: (value) {
                      if (_required(value, 'email') != null) return _required(value, 'email');
                      return RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(value!.trim()) ? null : 'Enter a valid email address.';
                    }),
                    const SizedBox(height: CeylonSpacing.md),
                    TextFormField(controller: _passwordController, obscureText: true, decoration: const InputDecoration(labelText: 'Password'), validator: (value) => value == null || value.length < 8 ? 'Use at least 8 characters.' : null),
                    if (_error != null) ...[
                      const SizedBox(height: CeylonSpacing.md),
                      Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                    ],
                    const SizedBox(height: CeylonSpacing.lg),
                    ElevatedButton(onPressed: _isSubmitting ? null : _submit, child: _isSubmitting ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white)) : const Text('Create account')),
                    const SizedBox(height: CeylonSpacing.sm),
                    TextButton(onPressed: _isSubmitting ? null : () => context.go('/login'), child: const Text('Already have an account? Sign in')),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    ),
  );
}
