import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../services/auth_service.dart';
import '../theme/app_theme.dart';
import '../widgets/auth_scope.dart';
import '../widgets/brand_mark.dart';

class LoginPage extends StatefulWidget {
  const LoginPage({super.key});

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _isSubmitting = false;
  String? _error;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  AuthService get _authService => AuthScope.of(context);

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _isSubmitting = true;
      _error = null;
    });
    final succeeded = await _authService.login(
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

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: CeylonColors.ivory,
    body: SafeArea(
      child: LayoutBuilder(
        builder: (context, constraints) => SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(
            CeylonSpacing.lg,
            CeylonSpacing.md,
            CeylonSpacing.lg,
            CeylonSpacing.xl,
          ),
          child: ConstrainedBox(
            constraints: BoxConstraints(
              minHeight: constraints.maxHeight - CeylonSpacing.lg,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const BrandLockup(),
                const SizedBox(height: CeylonSpacing.lg),
                ClipRRect(
                  borderRadius: BorderRadius.circular(CeylonRadii.card),
                  child: SizedBox(
                    height: 190,
                    child: Stack(
                      fit: StackFit.expand,
                      children: [
                        Image.asset(
                          'assets/images/sigiriya-hero.png',
                          fit: BoxFit.cover,
                          semanticLabel: 'Sigiriya landscape in Sri Lanka',
                        ),
                        DecoratedBox(
                          decoration: BoxDecoration(
                            gradient: LinearGradient(
                              begin: Alignment.topCenter,
                              end: Alignment.bottomCenter,
                              colors: [
                                CeylonColors.forestDeep.withValues(alpha: 0.18),
                                CeylonColors.forestDeep.withValues(alpha: 0.9),
                              ],
                            ),
                          ),
                        ),
                        Positioned(
                          left: 18,
                          right: 18,
                          bottom: 18,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'CeylonTrail AI',
                                style: Theme.of(context).textTheme.labelSmall
                                    ?.copyWith(
                                      color: CeylonColors.mint,
                                      fontWeight: FontWeight.w800,
                                      letterSpacing: 1.5,
                                    ),
                              ),
                              const SizedBox(height: 6),
                              Text(
                                'Every route begins with a sense of place.',
                                style: Theme.of(context)
                                    .textTheme
                                    .headlineMedium
                                    ?.copyWith(color: Colors.white),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: CeylonSpacing.xl),
                Text(
                  'Welcome back',
                  style: Theme.of(context).textTheme.displaySmall,
                ),
                const SizedBox(height: CeylonSpacing.sm),
                Text(
                  'Sign in to continue your shared workspace.',
                  style: Theme.of(context).textTheme.bodyLarge,
                ),
                const SizedBox(height: CeylonSpacing.lg),
                Form(
                  key: _formKey,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      TextFormField(
                        controller: _emailController,
                        keyboardType: TextInputType.emailAddress,
                        autofillHints: const [AutofillHints.username],
                        decoration: const InputDecoration(
                          labelText: 'Email',
                          prefixIcon: Icon(Icons.mail_outline),
                        ),
                        validator: (value) =>
                            value == null || value.trim().isEmpty
                            ? 'Enter your email.'
                            : null,
                      ),
                      const SizedBox(height: CeylonSpacing.md),
                      TextFormField(
                        controller: _passwordController,
                        obscureText: true,
                        autofillHints: const [AutofillHints.password],
                        decoration: const InputDecoration(
                          labelText: 'Password',
                          prefixIcon: Icon(Icons.lock_outline),
                        ),
                        validator: (value) => value == null || value.isEmpty
                            ? 'Enter your password.'
                            : null,
                      ),
                      if (_error != null) ...[
                        const SizedBox(height: CeylonSpacing.md),
                        Text(
                          _error!,
                          style: TextStyle(
                            color: Theme.of(context).colorScheme.error,
                          ),
                        ),
                      ],
                      const SizedBox(height: CeylonSpacing.lg),
                      ElevatedButton(
                        onPressed: _isSubmitting ? null : _submit,
                        child: _isSubmitting
                            ? const SizedBox(
                                width: 22,
                                height: 22,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                  color: Colors.white,
                                ),
                              )
                            : const Text('Sign in'),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    ),
  );
}
