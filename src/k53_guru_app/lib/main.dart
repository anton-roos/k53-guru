import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'domain/licence_code.dart';
import 'presentation/onboarding/licence_code_provider.dart';
import 'presentation/onboarding/licence_code_selection_screen.dart';
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
/// setting for now -- a dedicated profile toggle is Story 4.6's job) and
/// doubles as the app's router across three states (Story 4.3 introduced
/// the first two; Story 4.5 adds the third):
///
/// 1. No persisted learner profile id -> [StartLearningScreen] (first run).
/// 2. A profile id exists (freshly generated OR restored, Story 4.4) but no
///    licence code has been chosen yet -> [LicenceCodeSelectionScreen].
///    Licence code is a purely device-local setting, never restored/synced,
///    so this applies uniformly to a restored profile too.
/// 3. Both a profile id and a licence code exist -> the three-tab
///    [AppShell] (a returning learner goes straight in, no repeat
///    friction).
class K53GuruApp extends ConsumerWidget {
  const K53GuruApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<String?> profile = ref.watch(learnerProfileProvider);
    final AsyncValue<LicenceCode?> licenceCode = ref.watch(licenceCodeProvider);

    return MaterialApp(
      title: 'K53 Guru',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      themeMode: ThemeMode.system,
      home: profile.maybeWhen(
        data: (String? id) => id == null
            ? const StartLearningScreen()
            : licenceCode.maybeWhen(
                data: (LicenceCode? code) => code == null
                    ? const LicenceCodeSelectionScreen()
                    : const AppShell(),
                orElse: () => const LaunchingScreen(),
              ),
        orElse: () => const LaunchingScreen(),
      ),
    );
  }
}

/// Shown only for the brief instant it takes [learnerProfileProvider] and/or
/// [licenceCodeProvider] to read their persisted values off local storage,
/// before the router above can decide between [StartLearningScreen],
/// [LicenceCodeSelectionScreen], and [AppShell]. Local storage reads are
/// expected to be effectively instant in practice, so this is expected not
/// to be a user-perceptible splash screen -- though that has not been
/// independently verified by a test that inspects the very first frame.
class LaunchingScreen extends StatelessWidget {
  const LaunchingScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const Scaffold(body: Center(child: CircularProgressIndicator()));
  }
}
