import 'dart:async';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import 'brand_mark.dart';

class _AuthDestination {
  const _AuthDestination(this.asset, this.label, this.altText);
  final String asset;
  final String label;
  final String altText;
}

class AuthVisualShell extends StatefulWidget {
  const AuthVisualShell({required this.title, required this.subtitle, required this.form, required this.footer, super.key});

  final String title;
  final String subtitle;
  final Widget form;
  final Widget footer;

  @override
  State<AuthVisualShell> createState() => _AuthVisualShellState();
}

class _AuthVisualShellState extends State<AuthVisualShell> {
  static const _destinations = [
    _AuthDestination('assets/images/destinations/ella/02.jpg', 'ELLA · UVA', 'Train crossing a bridge through Ella’s tea country in Sri Lanka'),
    _AuthDestination('assets/images/destinations/sigiriya/03.jpg', 'SIGIRIYA · CENTRAL PROVINCE', 'Sigiriya rock fortress in Sri Lanka'),
    _AuthDestination('assets/images/destinations/bentota/01.jpg', 'BENTOTA · SOUTHWEST COAST', 'Bentota lagoon and fishing boats on Sri Lanka’s southwest coast'),
    _AuthDestination('assets/images/destinations/udawalawe/03.jpg', 'UDAWALAWE · SOUTHERN SRI LANKA', 'Spotted deer in Udawalawe National Park, Sri Lanka'),
    _AuthDestination('assets/images/destinations/galle_fort/02.jpg', 'GALLE FORT · SOUTHERN COAST', 'Galle Fort clock tower on Sri Lanka’s southern coast'),
  ];

  Timer? _timer;
  int _activeIndex = 0;

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(const Duration(seconds: 6), (_) {
      if (mounted) setState(() => _activeIndex = (_activeIndex + 1) % _destinations.length);
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: CeylonColors.forestDeep,
    body: LayoutBuilder(
      builder: (context, constraints) {
        final isWide = constraints.maxWidth >= 760;
        final experience = _AuthExperience(widget: widget, active: _destinations[_activeIndex], activeIndex: _activeIndex);
        if (!isWide) return SafeArea(child: experience);
        return Stack(
          fit: StackFit.expand,
          children: [
            ImageFiltered(
              imageFilter: ui.ImageFilter.blur(sigmaX: 22, sigmaY: 22),
              child: Image.asset(_destinations[_activeIndex].asset, fit: BoxFit.cover, filterQuality: FilterQuality.high),
            ),
            ColoredBox(color: CeylonColors.forestDeep.withValues(alpha: .58)),
            SafeArea(child: Center(child: experience)),
          ],
        );
      },
    ),
  );
}

class _AuthExperience extends StatelessWidget {
  const _AuthExperience({required this.widget, required this.active, required this.activeIndex});

  final AuthVisualShell widget;
  final _AuthDestination active;
  final int activeIndex;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) {
      final isShortViewport = constraints.maxHeight < 680;
      final heroHeight = constraints.maxHeight.isFinite
          ? (constraints.maxHeight * .54).clamp(280.0, 390.0)
          : 390.0;
      final overlap = isShortViewport ? 0.0 : 78.0;

      return ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 640),
        child: SingleChildScrollView(
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          child: Column(
            children: [
              SizedBox(
            height: heroHeight,
            child: Stack(
              fit: StackFit.expand,
              children: [
                AnimatedSwitcher(
                  duration: const Duration(milliseconds: 900),
                  switchInCurve: Curves.easeOut,
                  switchOutCurve: Curves.easeIn,
                  child: _AuthHeroPhoto(destination: active, key: ValueKey(active.asset)),
                ),
                DecoratedBox(
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      begin: Alignment.topCenter,
                      end: Alignment.bottomCenter,
                      colors: [CeylonColors.forestDeep.withValues(alpha: .12), CeylonColors.forestDeep.withValues(alpha: .76)],
                    ),
                  ),
                ),
                Positioned(
                  top: 22,
                  left: 22,
                  right: 22,
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const _LightBrandLockup(),
                      Text(active.label, style: const TextStyle(color: Colors.white70, fontSize: 9, fontWeight: FontWeight.w800, letterSpacing: 1.05)),
                    ],
                  ),
                ),
                Positioned(
                  left: 24,
                  right: 24,
                  bottom: 112,
                  child: Text('Discover Sri Lanka,\nyour way.', style: Theme.of(context).textTheme.headlineMedium?.copyWith(color: Colors.white, fontSize: 32, height: 1.06)),
                ),
                Positioned(
                  left: 24,
                  bottom: 88,
                  child: Row(children: List.generate(5, (index) => AnimatedContainer(duration: const Duration(milliseconds: 250), margin: const EdgeInsets.only(right: 5), width: index == activeIndex ? 22 : 5, height: 5, decoration: BoxDecoration(color: Colors.white.withValues(alpha: index == activeIndex ? .95 : .48), borderRadius: BorderRadius.circular(5))))),
                ),
              ],
            ),
          ),
          Transform.translate(
            offset: Offset(0, -overlap),
            child: Container(
              width: double.infinity,
              padding: const EdgeInsets.fromLTRB(24, 32, 24, 22),
              decoration: const BoxDecoration(color: CeylonColors.ivory, borderRadius: BorderRadius.only(topLeft: Radius.circular(48), topRight: Radius.circular(22))),
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                Text(widget.title, style: Theme.of(context).textTheme.displaySmall?.copyWith(fontSize: 30)),
                const SizedBox(height: 6),
                Text(widget.subtitle, style: Theme.of(context).textTheme.bodyMedium),
                const SizedBox(height: 24),
                widget.form,
                const SizedBox(height: 12),
                widget.footer,
              ]),
            ),
              ),
            ],
          ),
        ),
      );
    },
  );
}

class _AuthHeroPhoto extends StatelessWidget {
  const _AuthHeroPhoto({required this.destination, super.key});

  final _AuthDestination destination;

  @override
  Widget build(BuildContext context) => Stack(
    fit: StackFit.expand,
    children: [
      ImageFiltered(
        imageFilter: ui.ImageFilter.blur(sigmaX: 18, sigmaY: 18),
        child: Image.asset(destination.asset, fit: BoxFit.cover, filterQuality: FilterQuality.high),
      ),
      ColoredBox(color: CeylonColors.forestDeep.withValues(alpha: .24)),
      Image.asset(destination.asset, fit: BoxFit.contain, filterQuality: FilterQuality.high, semanticLabel: destination.altText),
    ],
  );
}

class _LightBrandLockup extends StatelessWidget {
  const _LightBrandLockup();

  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      ColorFiltered(colorFilter: const ColorFilter.mode(Colors.white, BlendMode.srcIn), child: const BrandMark(size: 34)),
      const SizedBox(width: 8),
      const Text('CeylonTrail', style: TextStyle(color: Colors.white, fontFamily: 'Playfair Display', fontSize: 20, fontWeight: FontWeight.w600)),
    ],
  );
}
