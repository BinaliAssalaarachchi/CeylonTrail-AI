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
    _AuthDestination('assets/images/sigiriya-hero.png', 'SIGIRIYA · CENTRAL PROVINCE', 'Sigiriya rock and tropical landscape in Sri Lanka'),
    _AuthDestination('assets/images/ella-hills.jpg', 'ELLA · UVA', 'Misty green hills in Ella, Sri Lanka'),
    _AuthDestination('assets/images/mirissa-coast.jpg', 'MIRISSA · SOUTHERN COAST', 'Mirissa beach on Sri Lanka’s southern coast'),
    _AuthDestination('assets/images/yala-wildlife.jpg', 'YALA · SOUTHERN SRI LANKA', 'Landscape in Yala National Park, Sri Lanka'),
    _AuthDestination('assets/images/tea-country-hero.jpg', 'TEA COUNTRY · HIGHLANDS', 'Sri Lankan tea country hills'),
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
              child: Image.asset(_destinations[_activeIndex].asset, fit: BoxFit.cover),
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
  Widget build(BuildContext context) => ConstrainedBox(
    constraints: const BoxConstraints(maxWidth: 640),
    child: SingleChildScrollView(
      keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
      child: Column(
        children: [
          SizedBox(
            height: 390,
            child: Stack(
              fit: StackFit.expand,
              children: [
                AnimatedSwitcher(
                  duration: const Duration(milliseconds: 900),
                  switchInCurve: Curves.easeOut,
                  switchOutCurve: Curves.easeIn,
                  child: Image.asset(active.asset, key: ValueKey(active.asset), fit: BoxFit.cover, semanticLabel: active.altText),
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
            offset: const Offset(0, -78),
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
