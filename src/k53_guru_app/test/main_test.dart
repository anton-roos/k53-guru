// Verifies `main.dart`'s portrait-only orientation lock -- Story 4.2's
// "Always: Lock orientation to portrait only (`SystemChrome.
// setPreferredOrientations` with `portraitUp`/`portraitDown`) at app
// startup, before `runApp`" boundary, which previously had zero automated
// coverage: nothing invoked `main()` or mocked the platform channel it
// calls, so a regression dropping/reordering the call, or passing the
// wrong orientation list, would go completely undetected.
//
// `SystemChrome.setPreferredOrientations` sends a
// `SystemChrome.setPreferredOrientations` method call (with the requested
// orientations serialized via `Enum.toString()`, e.g.
// `'DeviceOrientation.portraitUp'`) over `SystemChannels.platform`, a JSON
// `MethodChannel`. Mocking that channel via
// `TestDefaultBinaryMessengerBinding` lets this test capture the exact
// call `main()` makes without needing any platform plugin.
//
// `main()` is invoked directly (rather than extracting the orientation
// call into a separately-testable function) since this story's tests are
// test-only changes and must not touch `lib/`. `main()` does go on to
// build the real `K53GuruApp` (via `runApp`, synchronously during this
// call thanks to `AutomatedTestWidgetsFlutterBinding.scheduleWarmUpFrame`).
// Since Story 4.3, `K53GuruApp` is also the first-run router: it reads the
// persisted learner profile id via `learnerProfileProvider`, which in this
// unmocked test environment fails to reach any `SharedPreferences`
// platform implementation -- `LearnerProfileStore` treats that failure as
// "no profile id" per its own contract, so the router settles on
// `StartLearningScreen` rather than `AppShell`, and no network request for
// the Practice tab's sittings ever fires. Either way, the orientation call
// this test cares about is awaited and captured *before* `runApp` even
// runs, and the test never awaits/pumps further, so which screen `main()`
// ends up building has no bearing on this test's outcome.
import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/domain/available_sitting.dart';
import 'package:k53_guru_app/domain/licence_code.dart';
import 'package:k53_guru_app/main.dart' as app;
import 'package:k53_guru_app/main.dart';
import 'package:k53_guru_app/presentation/onboarding/licence_code_provider.dart';
import 'package:k53_guru_app/presentation/onboarding/learner_profile_provider.dart';
import 'package:k53_guru_app/presentation/settings/theme_mode_provider.dart';
import 'package:k53_guru_app/presentation/shell/app_shell.dart';
import 'package:k53_guru_app/presentation/sittings/sittings_list_provider.dart';

/// A [ThemeModeNotifier] whose [build] is fully controlled by the test, so
/// the root [MaterialApp]'s `themeMode:` can be driven into a specific
/// `AsyncValue` state without touching real `SharedPreferences` --
/// mirroring `k53_guru_app_router_test.dart`'s `_FakeLearnerProfileNotifier`/
/// `_FakeLicenceCodeNotifier` pattern.
class _FakeThemeModeNotifier extends ThemeModeNotifier {
  _FakeThemeModeNotifier(this._build);

  final Future<ThemeMode> Function() _build;

  @override
  Future<ThemeMode> build() => _build();
}

void main() {
  testWidgets(
      'main() locks the app to portrait-only orientation before runApp',
      (WidgetTester tester) async {
    final List<MethodCall> platformCalls = <MethodCall>[];

    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(SystemChannels.platform, (
      MethodCall call,
    ) async {
      platformCalls.add(call);
      return null;
    });
    addTearDown(() {
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(SystemChannels.platform, null);
    });

    await app.main();

    final MethodCall orientationCall = platformCalls.singleWhere(
      (MethodCall call) =>
          call.method == 'SystemChrome.setPreferredOrientations',
      orElse: () => throw StateError(
        'main() never called SystemChrome.setPreferredOrientations',
      ),
    );

    expect(
      orientationCall.arguments,
      <String>['DeviceOrientation.portraitUp', 'DeviceOrientation.portraitDown'],
    );
  });

  // Story 4.6: `MaterialApp.themeMode` reads `themeModeProvider` instead of
  // hardcoding `ThemeMode.system`.

  Widget wrapWithThemeMode(Future<ThemeMode> Function() themeModeBuild) {
    return ProviderScope(
      overrides: [
        themeModeProvider.overrideWith(
          () => _FakeThemeModeNotifier(themeModeBuild),
        ),
        // Resolved so the router settles on AppShell quickly and
        // deterministically -- this test only cares about `themeMode:`,
        // not which screen is `home`.
        learnerProfileProvider.overrideWith(
          () => _StubLearnerProfileNotifier(),
        ),
        licenceCodeProvider.overrideWith(() => _StubLicenceCodeNotifier()),
        availableSittingsProvider.overrideWith(
          (Ref ref) => Future<List<AvailableSitting>>.value(
            const <AvailableSitting>[],
          ),
        ),
      ],
      child: const K53GuruApp(),
    );
  }

  testWidgets(
      'Overriding themeModeProvider to resolve to ThemeMode.dark results in '
      'MaterialApp.themeMode == ThemeMode.dark (not ThemeMode.system)',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      wrapWithThemeMode(() => Future<ThemeMode>.value(ThemeMode.dark)),
    );
    await tester.pumpAndSettle();

    final MaterialApp materialApp =
        tester.widget<MaterialApp>(find.byType(MaterialApp));
    expect(materialApp.themeMode, ThemeMode.dark);
  });

  testWidgets(
      'themeModeProvider left in AsyncLoading falls back to ThemeMode.light '
      'without crashing', (WidgetTester tester) async {
    // A Completer whose future never completes keeps the provider in
    // AsyncLoading for the lifetime of the test -- pump a single frame
    // rather than pumpAndSettle, which would hang waiting for it.
    await tester.pumpWidget(
      wrapWithThemeMode(() => Completer<ThemeMode>().future),
    );
    await tester.pump();

    final MaterialApp materialApp =
        tester.widget<MaterialApp>(find.byType(MaterialApp));
    expect(materialApp.themeMode, ThemeMode.light);
  });

  // Genuine end-to-end proof that tapping Dark in the real Profile screen
  // re-themes the real root MaterialApp -- same ProviderScope, same
  // themeModeProvider instance, no fake notifiers and no bespoke wrapping
  // MaterialApp. This closes the gap between `profile_screen_test.dart`'s
  // Dark-tap tests (which mount ProfileScreen inside a throwaway MaterialApp
  // that never reads themeModeProvider for its own `themeMode:`) and this
  // file's other dark-mode test above (which proves the root MaterialApp
  // follows themeModeProvider, but only via a direct override, never a real
  // tap) -- neither, on its own, proves the two halves are actually wired
  // together in a live app.
  testWidgets(
      'Tapping Dark in the real Profile screen (reached via the real '
      'K53GuruApp router and AppShell) re-themes the real root MaterialApp',
      (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_profile_id': '11111111-2222-4333-8444-555555555555',
      'learner_licence_code': 'Code1',
    });

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          // Only override needed: AppShell's Practice tab is built
          // immediately (IndexedStack builds every tab up front) and would
          // otherwise fire a real HTTP call. learnerProfileProvider,
          // licenceCodeProvider, and themeModeProvider are all left as the
          // real, SharedPreferences-backed implementations.
          availableSittingsProvider.overrideWith(
            (Ref ref) => Future<List<AvailableSitting>>.value(
              const <AvailableSitting>[],
            ),
          ),
        ],
        child: const K53GuruApp(),
      ),
    );
    await tester.pumpAndSettle();

    // The real router landed on AppShell -- an already-onboarded learner
    // (profile id + licence code both persisted).
    expect(find.byType(AppShell), findsOneWidget);

    // Navigate to the Profile tab via the real NavigationBar.
    await tester.tap(
      find.descendant(
        of: find.byType(NavigationBar),
        matching: find.text('Profile'),
      ),
    );
    await tester.pumpAndSettle();

    // Tap the real "Dark" segment in the real SegmentedButton.
    await tester.ensureVisible(find.text('Dark'));
    await tester.tap(find.text('Dark'));
    await tester.pumpAndSettle();

    final MaterialApp materialApp =
        tester.widget<MaterialApp>(find.byType(MaterialApp));
    expect(
      materialApp.themeMode,
      ThemeMode.dark,
      reason: 'tapping Dark in the real Profile screen must re-theme the '
          'real root MaterialApp -- proving themeModeProvider genuinely '
          'wires the two together end to end',
    );
  });
}

/// Resolves immediately to an existing profile id, so tests that only care
/// about `themeMode:` land on [AppShell] without extra ceremony.
class _StubLearnerProfileNotifier extends LearnerProfileNotifier {
  @override
  Future<String?> build() =>
      Future<String?>.value('11111111-2222-4333-8444-555555555555');
}

/// Resolves immediately to an already-chosen licence code, mirroring
/// [_StubLearnerProfileNotifier].
class _StubLicenceCodeNotifier extends LicenceCodeNotifier {
  @override
  Future<LicenceCode?> build() => Future<LicenceCode?>.value(LicenceCode.code1);
}
