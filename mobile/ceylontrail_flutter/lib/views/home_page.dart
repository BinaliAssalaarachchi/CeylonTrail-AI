import 'package:flutter/material.dart';

import '../widgets/auth_scope.dart';

class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final authService = AuthScope.of(context);
    final user = authService.user!;
    return Scaffold(
      appBar: AppBar(
        title: const Text('CeylonTrail AI'),
        actions: [
          IconButton(
            tooltip: 'Log out',
            onPressed: () => authService.logout(),
            icon: const Icon(Icons.logout),
          ),
        ],
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Card(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Welcome, ${user.displayName}.', style: Theme.of(context).textTheme.headlineSmall),
                  const SizedBox(height: 16),
                  Text(user.email),
                  const SizedBox(height: 8),
                  Chip(label: Text(user.role)),
                  const SizedBox(height: 20),
                  const Text('Shared authentication is ready. Feature modules will appear here later.'),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
