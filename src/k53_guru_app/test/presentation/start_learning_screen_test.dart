// Covers all 4 rows of spec-4-3's I/O & Edge-Case Matrix, driving the real
// `K53GuruApp` router (main.dart) end-to-end -- SharedPreferences ->
// LearnerProfileStore -> learnerProfileProvider -> the router's choice of
// StartLearningScreen vs AppShell -- rather than stubbing any of those
// layers. Only `availableSittingsProvider` is overridden, to keep AppShell's
// Practice tab network-free once it renders.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:shared_preferences_platform_interface/shared_preferences_platform_interface.dart';

import 'package:k53_guru_app/data/local/learner_profile_store.dart';
import 'package:k53_guru_app/domain/available_sitting.dart';
import 'package:k53_guru_app/main.dart';
import 'package:k53_guru_app/presentation/onboarding/learner_profile_provider.dart';
import 'package:k53_guru_app/presentation/onboarding/start_learning_screen.dart';
import 'package:k53_guru_app/presentation/shell/app_shell.dart';
import 'package:k53_guru_app/presentation/sittings/sittings_list_provider.dart';

const String _profileIdKey = 'learner_profile_id';

/// A [SharedPreferencesStorePlatform] that fails every operation --
/// deterministically reproducing the spec's "`SharedPreferences` throws"
/// edge case, unlike leaving no platform implementation registered at all
/// (which, on this plugin version, leaves the read pending forever rather
/// than throwing, hanging any `pumpAndSettle` that waits on it).
class _ThrowingSharedPreferencesStore extends SharedPreferencesStorePlatform {
  @override
  bool get isMock => true;

  @override
  Future<Map<String, Object>> getAll() =>
      throw Exception('local storage unavailable');

  @override
  Future<bool> setValue(String valueType, String key, Object value) =>
      throw Exception('local storage unavailable');

  @override
  Future<bool> remove(String key) =>
      throw Exception('local storage unavailable');

  @override
  Future<bool> clear() => throw Exception('local storage unavailable');
}

Widget _app() {
  return ProviderScope(
    overrides: [
      availableSittingsProvider.overrideWith(
        (Ref ref) => Future<List<AvailableSitting>>.value(
          const <AvailableSitting>[],
        ),
      ),
    ],
    child: const K53GuruApp(),
  );
}

final RegExp _uuidV4Pattern = RegExp(
  r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
);

void main() {
  // This test *must* run before any other test in this file calls
  // `SharedPreferences.getInstance()` or `setMockInitialValues` --
  // `SharedPreferences` memoizes its first successful read in a private
  // static completer that only `setMockInitialValues` (which installs a
  // *working* in-memory store, the opposite of what this test needs)
  // resets. Running first guarantees that completer is still unset, so the
  // throwing store below is actually what the app's first read hits.
  testWidgets(
      'Local storage read fails -> treated as no profile id (first-run '
      'path), never crashes', (WidgetTester tester) async {
    SharedPreferencesStorePlatform.instance = _ThrowingSharedPreferencesStore();
    addTearDown(() {
      // Leave a normal, empty mock store behind for every test that runs
      // after this one.
      SharedPreferences.setMockInitialValues(<String, Object>{});
    });

    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.byType(StartLearningScreen), findsOneWidget);
    expect(find.byType(AppShell), findsNothing);
  });

  testWidgets(
      'Fresh install, first launch -> StartLearningScreen renders',
      (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();

    expect(find.byType(StartLearningScreen), findsOneWidget);
    expect(find.text('Start learning'), findsOneWidget);
    expect(find.byType(AppShell), findsNothing);
  });

  testWidgets(
      'Tap Start learning -> a new UUID v4 is generated, persisted, and '
      'the app navigates to AppShell', (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();
    expect(find.byType(StartLearningScreen), findsOneWidget);

    await tester.tap(find.text('Start learning'));
    await tester.pumpAndSettle();

    expect(find.byType(AppShell), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);

    // Actually persisted (not just held in memory) -- a fresh read finds
    // the same value a real relaunch would use.
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    final String? persisted = prefs.getString(_profileIdKey);
    expect(persisted, isNotNull);
    expect(
      _uuidV4Pattern.hasMatch(persisted!),
      isTrue,
      reason: 'persisted id must be a UUID v4, got "$persisted"',
    );

    // The single app-wide provider any future screen's `K53ApiClient` call
    // (Epic 5/6) would read exposes this exact same value -- not a
    // different in-memory copy.
    final BuildContext appShellContext =
        tester.element(find.byType(AppShell));
    final AsyncValue<String?> providerState = ProviderScope.containerOf(
      appShellContext,
    ).read(learnerProfileProvider);
    expect(providerState.value, persisted);
  });

  testWidgets(
      'Second launch, same device -> a persisted profile id sends the app '
      'straight into AppShell, no StartLearningScreen shown again',
      (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      _profileIdKey: '11111111-2222-4333-8444-555555555555',
    });

    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();

    expect(find.byType(AppShell), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
  });

  testWidgets(
      'Rapid double-tap on Start learning generates only one profile id',
      (WidgetTester tester) async {
    // The real LearnerProfileStore/SharedPreferences mock resolves the
    // write within a single microtask turn -- too fast for a real double
    // tap's race window to ever be observable via `pump()`. This fake
    // notifier holds `generateAndPersistProfileId()` open on a
    // test-controlled gate so the "in flight" window can be inspected
    // deterministically, while still counting how many times generation
    // was actually invoked -- the thing the guard must keep at 1.
    final _ManuallyGatedLearnerProfileNotifier notifier =
        _ManuallyGatedLearnerProfileNotifier();

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          learnerProfileProvider.overrideWith(() => notifier),
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
    expect(find.byType(StartLearningScreen), findsOneWidget);

    // First tap starts generation and immediately blocks on the gate --
    // one frame is enough for the screen's local `_isGenerating` flag to
    // rebuild the button as disabled.
    await tester.tap(find.text('Start learning'));
    await tester.pump();

    expect(notifier.generateCallCount, 1);
    final ElevatedButton button = tester.widget<ElevatedButton>(
      find.byType(ElevatedButton),
    );
    expect(
      button.onPressed,
      isNull,
      reason: 'button must disable itself while generation is in flight',
    );

    // Second tap lands on the now-disabled button and must be a no-op --
    // it must not start a second, concurrent generation.
    await tester.tap(find.text('Start learning'));
    await tester.pump();
    expect(
      notifier.generateCallCount,
      1,
      reason: 'a tap on a disabled button must not start a second '
          'generateAndPersistProfileId() call',
    );

    // Release the gate and let the single in-flight generation finish.
    notifier.releaseGate();
    await tester.pumpAndSettle();

    expect(find.byType(AppShell), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);

    // Exactly one id was persisted -- not overwritten mid-flight by a
    // second generation racing the first.
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    final String? persisted = prefs.getString(_profileIdKey);
    expect(persisted, isNotNull);
    expect(
      _uuidV4Pattern.hasMatch(persisted!),
      isTrue,
      reason: 'persisted id must be a UUID v4, got "$persisted"',
    );
  });
}

/// A [LearnerProfileNotifier] whose [generateAndPersistProfileId] blocks on
/// a test-controlled gate (released via [releaseGate]) after entering the
/// loading state, and counts how many times it was actually invoked -- so
/// a widget test can deterministically observe the "generation in flight"
/// window that a real, near-instant mocked write resolves right through.
class _ManuallyGatedLearnerProfileNotifier extends LearnerProfileNotifier {
  int generateCallCount = 0;
  final Completer<void> _gate = Completer<void>();

  @override
  Future<String?> build() => Future<String?>.value();

  @override
  Future<String> generateAndPersistProfileId() async {
    generateCallCount++;
    // Deliberately does NOT set `state` to loading here -- the double-tap
    // guard under test lives entirely in `StartLearningScreen`'s own local
    // widget state, not in `learnerProfileProvider`'s AsyncValue, so this
    // provider's state stays AsyncData(null) (its build() result) for the
    // whole gated wait, exactly like the real notifier would.
    await _gate.future;
    const String id = '99999999-8888-4777-8666-555555555555';
    await const LearnerProfileStore().writeProfileId(id);
    state = const AsyncData<String?>(id);
    return id;
  }

  void releaseGate() => _gate.complete();
}
