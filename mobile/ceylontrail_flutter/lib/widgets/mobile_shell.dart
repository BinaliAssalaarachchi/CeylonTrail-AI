import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../theme/app_theme.dart';
import 'brand_mark.dart';

class MobileShell extends StatelessWidget {
  const MobileShell({required this.navigationShell, super.key});

  final StatefulNavigationShell navigationShell;

  static const _destinations = [
    NavigationDestination(
      icon: Icon(Icons.home_outlined),
      selectedIcon: Icon(Icons.home),
      label: 'Home',
    ),
    NavigationDestination(
      icon: Icon(Icons.explore_outlined),
      selectedIcon: Icon(Icons.explore),
      label: 'Discover',
    ),
    NavigationDestination(
      icon: Icon(Icons.route_outlined),
      selectedIcon: Icon(Icons.route),
      label: 'Trips',
    ),
    NavigationDestination(
      icon: Icon(Icons.bookmark_border),
      selectedIcon: Icon(Icons.bookmark),
      label: 'Bookings',
    ),
    NavigationDestination(
      icon: Icon(Icons.person_outline),
      selectedIcon: Icon(Icons.person),
      label: 'Profile',
    ),
  ];

  void _goToBranch(int index) => navigationShell.goBranch(
    index,
    initialLocation: index == navigationShell.currentIndex,
  );

  @override
  Widget build(BuildContext context) => Scaffold(
    body: navigationShell,
    bottomNavigationBar: NavigationBar(
      selectedIndex: navigationShell.currentIndex,
      onDestinationSelected: _goToBranch,
      destinations: _destinations,
    ),
  );
}

class MobileHeader extends StatelessWidget {
  const MobileHeader({required this.userName, this.onProfileTap, super.key});

  final String userName;
  final VoidCallback? onProfileTap;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisAlignment: MainAxisAlignment.spaceBetween,
    children: [
      const BrandLockup(compact: true),
      Semantics(
        button: true,
        label: 'Open profile',
        child: InkWell(
          onTap: onProfileTap,
          customBorder: const CircleBorder(),
          child: CircleAvatar(
            radius: 20,
            backgroundColor: CeylonColors.mint,
            child: Text(
              userName.isEmpty ? '?' : userName.characters.first.toUpperCase(),
              style: const TextStyle(
                color: CeylonColors.forest,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
        ),
      ),
    ],
  );
}
