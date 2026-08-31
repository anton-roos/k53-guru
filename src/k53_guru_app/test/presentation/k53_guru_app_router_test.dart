// Exercises `K53GuruApp`'s `home` routing logic (main.dart) directly, by
// forcing `learnerProfileProvider` into each of its possible `AsyncValue`
// states via a fake `LearnerProfileNotifier` override, rather than driving
// the state indirectly through real `SharedPreferences`/tap sequences (that
// end-to-end coverage already exists in `start_learning_screen_test.dart`).
//
// This is the only place `LaunchingScreen` (the `orElse` fallback) and the
// `AsyncError` branch are exercised at all.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:k53_guru_app/domain/available_sitting.dart';
import 'package:k53_guru_app/main.dart';
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

Widget _app(Future<String?> Function() build) {
  return ProviderScope(
    overrides: [
      learnerProfileProvider.overrideWith(
        () => _FakeLearnerProfileNotifier(build),
      ),
      // Only reached if the router picks AppShell (the AsyncData(non-null)
      // case) -- Practice's tab would otherwise fire a real HTTP call.
      availableSittingsProvider.overrideWith(
        (Ref ref) => Future<List<AvailableSitting>>.value(
          const <AvailableSitting>[],
        ),
      ),
    ],
    child: const K53GuruApp(),
  );
}

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
    expect(find.byType(AppShell), findsNothing);
  });

  testWidgets('AsyncData(null) (no profile id) -> StartLearningScreen renders',
      (WidgetTester tester) async {
    await tester.pumpWidget(_app(() => Future<String?>.value()));
    await tester.pumpAndSettle();

    expect(find.byType(StartLearningScreen), findsOneWidget);
    expect(find.byType(AppShell), findsNothing);
    expect(find.byType(LaunchingScreen), findsNothing);
  });

  testWidgets(
      "AsyncData('some-existing-uuid') -> AppShell renders",
      (WidgetTester tester) async {
    await tester.pumpWidget(
      _app(() => Future<String?>.value('11111111-2222-4333-8444-555555555555')),
    );
    await tester.pumpAndSettle();

    expect(find.byType(AppShell), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(LaunchingScreen), findsNothing);
  });

  testWidgets(
      'AsyncError -> falls through the same orElse branch as loading, so '
      'LaunchingScreen renders (pinning main.dart\'s actual maybeWhen '
      'behaviour, which has no dedicated error case)', (WidgetTester tester) async {
    await tester.pumpWidget(
      _app(() => Future<String?>.error(Exception('profile read failed'))),
    );
    await tester.pump();

    expect(find.byType(LaunchingScreen), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(AppShell), findsNothing);
  });
}
