import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'presentation/shell/app_shell.dart';
import 'theme/app_theme.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // K53 Guru is portrait-only, single-column per DESIGN.md -- locked before
  // `runApp` so no frame ever renders in landscape.
  await SystemChrome.setPreferredOrientations(<DeviceOrientation>[
    DeviceOrientation.portraitUp,
    DeviceOrientation.portraitDown,
  ]);
  runApp(const ProviderScope(child: K53GuruApp()));
}

/// App root. Wires the DESIGN.md theme (light/dark, following the system
/// setting for now -- a dedicated profile toggle is Story 4.6's job) and
/// launches the three-tab [AppShell].
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
      home: const AppShell(),
    );
  }
}
