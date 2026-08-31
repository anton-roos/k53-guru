import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'presentation/sittings/sittings_list_screen.dart';
import 'theme/app_theme.dart';

void main() {
  runApp(const ProviderScope(child: K53GuruApp()));
}

/// App root. Wires the DESIGN.md theme (light/dark, following the system
/// setting for now -- a dedicated profile toggle is Story 4.6's job) and
/// launches the one proof screen this story ships.
class K53GuruApp extends StatelessWidget {
  const K53GuruApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'K53 Guru',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      themeMode: ThemeMode.system,
      home: const SittingsListScreen(),
    );
  }
}
