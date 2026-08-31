import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'presentation/onboarding/learner_profile_provider.dart';
import 'presentation/onboarding/start_learning_screen.dart';
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
/// setting for now -- a dedicated profile toggle is Story 4.6's job) and,
/// per Story 4.3, doubles as the first-run router: it reads the persisted
/// learner profile id via [learnerProfileProvider] and launches either
/// [StartLearningScreen] (no id yet -- first run) or the three-tab
/// [AppShell] (a returning learner goes straight in, no repeat friction).
class K53GuruApp extends ConsumerWidget {
  const K53GuruApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<String?> profile = ref.watch(learnerProfileProvider);

    return MaterialApp(
      title: 'K53 Guru',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      themeMode: ThemeMode.system,
      home: profile.maybeWhen(
        data: (String? id) =>
            id == null ? const StartLearningScreen() : const AppShell(),
        orElse: () => const LaunchingScreen(),
      ),
    );
  }
}

/// Shown only for the brief instant it takes [learnerProfileProvider] to
/// read the persisted profile id off local storage, before the router
/// above can decide between [StartLearningScreen] and [AppShell]. Local
/// storage reads are expected to be effectively instant in practice, so
/// this is expected not to be a user-perceptible splash screen -- though
/// that has not been independently verified by a test that inspects the
/// very first frame.
class LaunchingScreen extends StatelessWidget {
  const LaunchingScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const Scaffold(body: Center(child: CircularProgressIndicator()));
  }
}
