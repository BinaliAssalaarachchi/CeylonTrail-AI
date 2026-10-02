import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../theme/app_theme.dart';
import '../widgets/brand_mark.dart';

class WelcomePage extends StatelessWidget {
  const WelcomePage({super.key});

  static const _heroAsset = 'assets/images/welcome_mobile.png';

  @override
  Widget build(BuildContext context) => Scaffold(
    body: LayoutBuilder(
      builder: (context, constraints) {
        final isDesktop = constraints.maxWidth >= 900;
        final contentWidth = isDesktop ? 560.0 : constraints.maxWidth;

        return Stack(
          fit: StackFit.expand,
          children: [
            Image.asset(
              _heroAsset,
              fit: BoxFit.cover,
              alignment: const Alignment(0.12, 0.0),
              filterQuality: FilterQuality.high,
              semanticLabel: 'Sigiriya rock fortress in Sri Lanka',
            ),
            const DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  colors: [
                    Color(0x3300241A),
                    Color(0x1200241A),
                    Color(0xE600241A),
                  ],
                  stops: [0.0, 0.42, 1.0],
                ),
              ),
            ),
            SafeArea(
              child: Align(
                alignment: Alignment.topCenter,
                child: ConstrainedBox(
                  constraints: BoxConstraints(maxWidth: contentWidth),
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(24, 22, 24, 28),
                    child: const _WelcomeContent(),
                  ),
                ),
              ),
            ),
          ],
        );
      },
    ),
  );
}

class _WelcomeContent extends StatelessWidget {
  const _WelcomeContent();

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) => SizedBox(
      height: MediaQuery.sizeOf(context).height - MediaQuery.paddingOf(context).vertical - 50,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const _WelcomeBrand(),
          const Spacer(),
          Text(
            'Your journey starts here.',
            style: Theme.of(context).textTheme.headlineMedium?.copyWith(
              color: Colors.white,
              fontSize: constraints.maxWidth < 380 ? 32 : 38,
              height: 1.05,
            ),
          ),
          const SizedBox(height: 12),
          const Text(
            'Discover Sri Lanka, one unforgettable journey at a time.',
            style: TextStyle(color: Colors.white70, fontSize: 16, height: 1.45),
          ),
          const SizedBox(height: 28),
          FilledButton(
            onPressed: () => context.push('/register'),
            style: FilledButton.styleFrom(
              backgroundColor: CeylonColors.ivory,
              foregroundColor: CeylonColors.forest,
              minimumSize: const Size.fromHeight(56),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
            ),
            child: const Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Text('Get started'),
                SizedBox(width: 8),
                Icon(Icons.arrow_forward_rounded, size: 19),
              ],
            ),
          ),
          const SizedBox(height: 14),
          Center(
            child: TextButton(
              onPressed: () => context.push('/login'),
              style: TextButton.styleFrom(foregroundColor: Colors.white),
              child: const Text.rich(
                TextSpan(
                  text: 'Already have an account?  ',
                  children: [
                    TextSpan(style: TextStyle(fontWeight: FontWeight.w800), text: 'Sign in'),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    ),
  );
}

class _WelcomeBrand extends StatelessWidget {
  const _WelcomeBrand();

  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      ColorFiltered(
        colorFilter: const ColorFilter.mode(Colors.white, BlendMode.srcIn),
        child: const BrandMark(size: 36),
      ),
      const SizedBox(width: 9),
      const Text(
        'CeylonTrail',
        style: TextStyle(
          color: Colors.white,
          fontFamily: 'Playfair Display',
          fontSize: 22,
          fontWeight: FontWeight.w600,
        ),
      ),
    ],
  );
}
