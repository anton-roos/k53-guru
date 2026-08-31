import 'package:flutter/material.dart';

import '../profile/profile_placeholder_screen.dart';
import '../sittings/sittings_list_screen.dart';
import '../test_mode/test_mode_placeholder_screen.dart';

/// The app's persistent bottom-navigation shell: exactly three destinations
/// -- Practice, Test, Profile -- backed by an `IndexedStack` so each tab's
/// subtree is built once and stays alive across switches (scroll position,
/// form state, in-flight requests all survive switching away and back)
/// rather than being rebuilt from scratch every time.
///
/// Practice reuses Story 4.1's [SittingsListScreen] as-is; Test and Profile
/// are placeholders until Epic 5/6 (and a later Epic 4 story for Profile)
/// build their real content.
class AppShell extends StatefulWidget {
  const AppShell({super.key});

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _selectedIndex = 0;

  static const List<Widget> _tabs = <Widget>[
    SittingsListScreen(),
    TestModePlaceholderScreen(),
    ProfilePlaceholderScreen(),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(index: _selectedIndex, children: _tabs),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _selectedIndex,
        onDestinationSelected: (int index) {
          setState(() {
            _selectedIndex = index;
          });
        },
        destinations: const <NavigationDestination>[
          NavigationDestination(
            icon: Icon(Icons.school_outlined),
            selectedIcon: Icon(Icons.school),
            label: 'Practice',
          ),
          NavigationDestination(
            icon: Icon(Icons.timer_outlined),
            selectedIcon: Icon(Icons.timer),
            label: 'Test',
          ),
          NavigationDestination(
            icon: Icon(Icons.person_outline),
            selectedIcon: Icon(Icons.person),
            label: 'Profile',
          ),
        ],
      ),
    );
  }
}
