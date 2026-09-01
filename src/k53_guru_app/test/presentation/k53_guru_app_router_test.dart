// Exercises `K53GuruApp`'s `home` routing logic (main.dart) directly, by
// forcing `learnerProfileProvider` and `licenceCodeProvider` into each of
// their possible `AsyncValue` states via fake notifier overrides, rather
// than driving the state indirectly through real `SharedPreferences`/tap
// sequences (that end-to-end coverage already exists in
// `start_learning_screen_test.dart`/`licence_code_selection_screen_test.dart`).
//
// This is the only place `LaunchingScreen` (the `orElse` fallback) and the
// `AsyncError` branch are exercised at all, and the only place all three of
// the router's states -- StartLearningScreen / LicenceCodeSelectionScreen /
// AppShell -- are pinned side by side against the same routing logic.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:k53_guru_app/domain/available_sitting.dart';
import 'package:k53_guru_app/domain/licence_code.dart';
import 'package:k53_guru_app/main.dart';
import 'package:k53_guru_app/presentation/onboarding/licence_code_provider.dart';
import 'package:k53_guru_app/presentation/onboarding/licence_code_selection_screen.dart';
import 'package:k53_guru_app/presentation/onboarding/learner_profile_provider.dart';
import 'package:k53_guru_app/presentation/onboarding/start_learning_screen.dart';
import 'package:k53_guru_app/presentation/shell/app_shell.dart';
import 'package:k53_guru_app/presentation/sittings/sittings_list_provider.dart';

/// A [LearnerProfileNotifier] whose [build] is fully controlled by the
/// test, so `K53GuruApp`'s router can be driven into each `AsyncValue`
/// state without touching real `SharedPreferences`.
class _FakeLearnerProfileNotifier extends LearnerProfileNotifier {
  _FakeLearnerProfileNotifier(this._build);

  final Future<String?> Function() _build;

  @override
  Future<String?> build() => _build();
}

/// A [LicenceCodeNotifier] whose [build] is fully controlled by the test,
/// mirroring [_FakeLearnerProfileNotifier].
class _FakeLicenceCodeNotifier extends LicenceCodeNotifier {
  _FakeLicenceCodeNotifier(this._build);

  final Future<LicenceCode?> Function() _build;

  @override
  Future<LicenceCode?> build() => _build();
}

Widget _app(
  Future<String?> Function() profileBuild, {
  // Defaults to an already-chosen code, so tests that only care about
  // exercising the profile-id branch (loading/error/StartLearningScreen)
  // don't also have to think about the licence-code branch -- they land on
  // AppShell exactly as before this story added the third state.
  Future<LicenceCode?> Function() licenceCodeBuild = _defaultLicenceCodeBuild,
}) {
  return ProviderScope(
    overrides: [
      learnerProfileProvider.overrideWith(
        () => _FakeLearnerProfileNotifier(profileBuild),
      ),
      licenceCodeProvider.overrideWith(
        () => _FakeLicenceCodeNotifier(licenceCodeBuild),
      ),
      // Only reached if the router picks AppShell (both AsyncData(non-null))
      // -- Practice's tab would otherwise fire a real HTTP call.
      availableSittingsProvider.overrideWith(
        (Ref ref) => Future<List<AvailableSitting>>.value(
          const <AvailableSitting>[],
        ),
      ),
    ],
    child: const K53GuruApp(),
  );
}

Future<LicenceCode?> _defaultLicenceCodeBuild() =>
    Future<LicenceCode?>.value(LicenceCode.code1);

void main() {
  testWidgets('Loading state -> a loading indicator is shown',
      (WidgetTester tester) async {
    // A Completer whose future never completes keeps the provider in
    // AsyncLoading for the lifetime of the test -- pump a single frame
    // rather than pumpAndSettle, which would hang waiting for it.
    await tester.pumpWidget(_app(() => Completer<String?>().future));
    await tester.pump();

    expect(find.byType(LaunchingScreen), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(LicenceCodeSelectionScreen), findsNothing);
    expect(find.byType(AppShell), findsNothing);
  });

  testWidgets('AsyncData(null) (no profile id) -> StartLearningScreen renders',
      (WidgetTester tester) async {
    await tester.pumpWidget(_app(() => Future<String?>.value()));
    await tester.pumpAndSettle();

    expect(find.byType(StartLearningScreen), findsOneWidget);
    expect(find.byType(LicenceCodeSelectionScreen), findsNothing);
    expect(find.byType(AppShell), findsNothing);
    expect(find.byType(LaunchingScreen), findsNothing);
  });

  testWidgets(
      "AsyncData('some-existing-uuid'), AsyncData(null) licence code -> "
      'LicenceCodeSelectionScreen renders (profile present, no code chosen '
      'yet -- Story 4.5\'s new third router state)', (WidgetTester tester) async {
    await tester.pumpWidget(
      _app(
        () => Future<String?>.value('11111111-2222-4333-8444-555555555555'),
        licenceCodeBuild: () => Future<LicenceCode?>.value(),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byType(LicenceCodeSelectionScreen), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(AppShell), findsNothing);
    expect(find.byType(LaunchingScreen), findsNothing);
  });

  testWidgets(
      "AsyncData('some-existing-uuid'), AsyncData(non-null) licence code -> "
      'AppShell renders', (WidgetTester tester) async {
    await tester.pumpWidget(
      _app(() => Future<String?>.value('11111111-2222-4333-8444-555555555555')),
    );
    await tester.pumpAndSettle();

    expect(find.byType(AppShell), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(LicenceCodeSelectionScreen), findsNothing);
    expect(find.byType(LaunchingScreen), findsNothing);
  });

  testWidgets(
      "AsyncData('some-existing-uuid') profile, AsyncLoading licence code -> "
      'falls through the same orElse branch, so LaunchingScreen renders '
      '(the two independent local-storage reads resolving at different '
      'times is a realistic race, not just the licence-code-only-error '
      'case already covered below)', (WidgetTester tester) async {
    // A Completer whose future never completes keeps licenceCodeProvider in
    // AsyncLoading for the lifetime of the test, mirroring the top
    // "Loading state" test's pattern but for the licence-code provider
    // specifically, while the profile provider has already resolved.
    await tester.pumpWidget(
      _app(
        () => Future<String?>.value('11111111-2222-4333-8444-555555555555'),
        licenceCodeBuild: () => Completer<LicenceCode?>().future,
      ),
    );
    await tester.pump();

    expect(find.byType(LaunchingScreen), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(LicenceCodeSelectionScreen), findsNothing);
    expect(find.byType(AppShell), findsNothing);
  });

  testWidgets(
      'AsyncError on profile -> falls through the same orElse branch as '
      'loading, so LaunchingScreen renders (pinning main.dart\'s actual '
      'maybeWhen behaviour, which has no dedicated error case)',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      _app(() => Future<String?>.error(Exception('profile read failed'))),
    );
    await tester.pump();

    expect(find.byType(LaunchingScreen), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(LicenceCodeSelectionScreen), findsNothing);
    expect(find.byType(AppShell), findsNothing);
  });

  testWidgets(
      'AsyncError on licence code (profile resolved) -> falls through the '
      'same orElse branch, so LaunchingScreen renders',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      _app(
        () => Future<String?>.value('11111111-2222-4333-8444-555555555555'),
        licenceCodeBuild: () =>
            Future<LicenceCode?>.error(Exception('licence code read failed')),
      ),
    );
    await tester.pump();

    expect(find.byType(LaunchingScreen), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(LicenceCodeSelectionScreen), findsNothing);
    expect(find.byType(AppShell), findsNothing);
  });
}
