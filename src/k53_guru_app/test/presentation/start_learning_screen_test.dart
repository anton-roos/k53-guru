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
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:shared_preferences_platform_interface/shared_preferences_platform_interface.dart';

import 'package:k53_guru_app/data/local/learner_profile_store.dart';
import 'package:k53_guru_app/domain/available_sitting.dart';
import 'package:k53_guru_app/main.dart';
import 'package:k53_guru_app/presentation/onboarding/learner_profile_provider.dart';
import 'package:k53_guru_app/presentation/onboarding/licence_code_selection_screen.dart';
import 'package:k53_guru_app/presentation/onboarding/start_learning_screen.dart';
import 'package:k53_guru_app/presentation/profile/restore_profile_screen.dart';
import 'package:k53_guru_app/presentation/shell/app_shell.dart';
import 'package:k53_guru_app/presentation/sittings/sittings_list_provider.dart';

const String _profileIdKey = 'learner_profile_id';
const String _licenceCodeKey = 'learner_licence_code';

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
      'the app navigates to LicenceCodeSelectionScreen (Story 4.5: a '
      'profile id alone is not enough to reach AppShell -- a licence code '
      "hasn't been chosen yet)", (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();
    expect(find.byType(StartLearningScreen), findsOneWidget);

    await tester.tap(find.text('Start learning'));
    await tester.pumpAndSettle();

    expect(find.byType(LicenceCodeSelectionScreen), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(AppShell), findsNothing);

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
    final BuildContext selectionScreenContext =
        tester.element(find.byType(LicenceCodeSelectionScreen));
    final AsyncValue<String?> providerState = ProviderScope.containerOf(
      selectionScreenContext,
    ).read(learnerProfileProvider);
    expect(providerState.value, persisted);

    // Picking a code proceeds the rest of the way into AppShell -- the
    // full first-run flow end to end.
    await tester.tap(find.text('Code 1'));
    await tester.pumpAndSettle();

    expect(find.byType(AppShell), findsOneWidget);
    expect(find.byType(LicenceCodeSelectionScreen), findsNothing);
    expect(prefs.getString(_licenceCodeKey), 'Code1');
  });

  testWidgets(
      'Tap Restore profile -> a real Navigator.push opens RestoreProfileScreen; '
      'entering a valid UUID and submitting restores it through the real '
      'router, swapping StartLearningScreen out for LicenceCodeSelectionScreen '
      '(Story 4.5: licence code is device-local, never restored, so a '
      'restored profile also routes through code selection)',
      (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    // `StartLearningScreen._openRestoreProfile` always constructs a plain
    // `const RestoreProfileScreen()` (no injected scanner controller), so
    // reaching it via the real button press -- unlike every other test in
    // this suite, which pushes `RestoreProfileScreen` directly with an
    // `autoStart: false` controller -- means its `MobileScannerController`
    // really does try to auto-start the camera. There is no real
    // camera/platform-channel backend in the widget-test environment, so
    // `MobileScannerPlatform.instance` is swapped for a fake for the
    // duration of this test (the same pattern this file already uses for
    // `SharedPreferencesStorePlatform` above) -- this only stands in for
    // camera hardware, it does not stub any app code, so the real
    // `StartLearningScreen` -> `RestoreProfileScreen` -> validator ->
    // `learnerProfileProvider` -> router-swap path is still exercised
    // end-to-end.
    final MobileScannerPlatform originalScannerPlatform =
        MobileScannerPlatform.instance;
    MobileScannerPlatform.instance = _FakeMobileScannerPlatform();
    addTearDown(() {
      MobileScannerPlatform.instance = originalScannerPlatform;
    });

    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();
    expect(find.byType(StartLearningScreen), findsOneWidget);

    await tester.tap(find.text('Restore profile'));
    await tester.pumpAndSettle();

    // The real push happened -- `RestoreProfileScreen` is now on top.
    expect(find.byType(RestoreProfileScreen), findsOneWidget);

    const String id = '44444444-5555-4666-8777-888888888888';
    await tester.enterText(
      find.widgetWithText(TextField, 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'),
      id,
    );
    final Finder restoreButton =
        find.widgetWithText(ElevatedButton, 'Restore');
    // The button sits below the fold of the default test viewport once the
    // camera-preview box, divider, and field are laid out above it.
    await tester.ensureVisible(restoreButton);
    await tester.pumpAndSettle();
    await tester.tap(restoreButton);
    await tester.pumpAndSettle();

    // The router swapped -- both the first-run screen and the restore
    // screen are gone, and `LicenceCodeSelectionScreen` is showing (the
    // restored profile still has no locally-persisted licence code).
    expect(find.byType(LicenceCodeSelectionScreen), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(RestoreProfileScreen), findsNothing);
    expect(find.byType(AppShell), findsNothing);

    // Actually persisted through the real store, not just held in memory.
    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readProfileId(), id);

    final SharedPreferences prefs = await SharedPreferences.getInstance();
    expect(prefs.getString(_profileIdKey), id);
  });

  testWidgets(
      'Second launch, same device -> a persisted profile id AND licence '
      'code send the app straight into AppShell, no StartLearningScreen or '
      'LicenceCodeSelectionScreen shown again',
      (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      _profileIdKey: '11111111-2222-4333-8444-555555555555',
      _licenceCodeKey: 'Code2',
    });

    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();

    expect(find.byType(AppShell), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(LicenceCodeSelectionScreen), findsNothing);
  });

  testWidgets(
      'Second launch, profile id persisted but no licence code yet -> '
      'LicenceCodeSelectionScreen renders, not AppShell',
      (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      _profileIdKey: '11111111-2222-4333-8444-555555555555',
    });

    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();

    expect(find.byType(LicenceCodeSelectionScreen), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(AppShell), findsNothing);
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

    // The profile id now exists, but no licence code has been chosen yet
    // -- the router lands on LicenceCodeSelectionScreen, not AppShell
    // (Story 4.5). This test's own concern -- the double-tap dedupe guard
    // -- is already fully proven by `generateCallCount` above.
    expect(find.byType(LicenceCodeSelectionScreen), findsOneWidget);
    expect(find.byType(StartLearningScreen), findsNothing);
    expect(find.byType(AppShell), findsNothing);

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

/// A [MobileScannerPlatform] stand-in for the widget-test environment, which
/// has no real camera or platform-channel backend.
///
/// Only stands in for camera hardware -- every other part of the
/// `StartLearningScreen` -> `RestoreProfileScreen` -> `ProfileRestoreValidator`
/// -> `learnerProfileProvider` -> router path stays real. [start] mirrors
/// what a device with no usable camera would report: a [MobileScannerException]
/// is thrown, which `MobileScannerController.start()` catches internally and
/// surfaces as `MobileScannerState.error` (`MobileScanner`'s own built-in
/// error view then renders in place of a live camera preview) -- it never
/// propagates out as an unhandled exception, so this does not fail the test.
///
/// The barcode/torch/zoom streams are read synchronously by
/// `MobileScannerController.start()` before the (fake) platform call, so
/// they need a real (if empty) implementation regardless of how [start]
/// resolves; [dispose] is called unconditionally by
/// `MobileScannerController.dispose()`, so it also needs a safe no-op
/// implementation.
class _FakeMobileScannerPlatform extends MobileScannerPlatform {
  @override
  Stream<BarcodeCapture?> get barcodesStream =>
      const Stream<BarcodeCapture?>.empty();

  @override
  Stream<TorchState> get torchStateStream => const Stream<TorchState>.empty();

  @override
  Stream<double> get zoomScaleStateStream => const Stream<double>.empty();

  @override
  Future<MobileScannerViewAttributes> start(StartOptions startOptions) {
    throw const MobileScannerException(
      errorCode: MobileScannerErrorCode.genericError,
      errorDetails: MobileScannerErrorDetails(
        message: 'No camera available in the widget-test environment.',
      ),
    );
  }

  @override
  Future<void> stop() async {}

  @override
  Future<void> pause() async {}

  @override
  Future<void> dispose() async {}

  @override
  Widget buildCameraView() => const SizedBox.shrink();
}
