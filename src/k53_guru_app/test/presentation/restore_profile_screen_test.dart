// Covers spec-4-4's I/O & Edge-Case Matrix rows "Restore via manual paste,
// valid format" and "Restore, invalid format" end-to-end through the real
// widget tree -- text field -> `_RestoreProfileScreenState` ->
// `ProfileRestoreValidator` -> `LearnerProfileStore`/`learnerProfileProvider`
// -- exactly as a learner pasting their UUID would.
//
// The QR-scan camera path itself is deliberately NOT exercised here: a
// `MobileScannerController` with `autoStart: false` is injected so the
// widget never calls into `mobile_scanner`'s platform channel (there is no
// real camera/plugin backend in the widget-test environment). That path's
// coverage lives in `profile_restore_validator_test.dart` instead, which
// tests the exact same shared validate-and-persist function a real scan
// result would also flow through.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/data/local/learner_profile_store.dart';
import 'package:k53_guru_app/presentation/profile/restore_profile_screen.dart';
import 'package:k53_guru_app/theme/app_theme.dart';

const String _profileIdKey = 'learner_profile_id';
const String _uuidFieldHint = 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx';

/// Pumps `RestoreProfileScreen` pushed on top of a base route (a bare
/// `Scaffold` labelled "base") -- mirroring how the real app always reaches
/// it via `Navigator.push` from `StartLearningScreen`, and giving a
/// successful restore something to pop back to.
///
/// The injected `MobileScannerController(autoStart: false)` means the
/// camera preview never calls into `mobile_scanner`'s platform channel.
Future<void> _pumpPushedRestoreScreen(WidgetTester tester) async {
  await tester.pumpWidget(
    ProviderScope(
      child: MaterialApp(
        theme: AppTheme.light(),
        home: const Scaffold(body: Center(child: Text('base'))),
      ),
    ),
  );

  final BuildContext context = tester.element(find.text('base'));
  Navigator.of(context).push(
    MaterialPageRoute<void>(
      builder: (_) => RestoreProfileScreen(
        scannerController: MobileScannerController(autoStart: false),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Finder _uuidField() => find.widgetWithText(TextField, _uuidFieldHint);

Finder _restoreButton() => find.widgetWithText(ElevatedButton, 'Restore');

/// Types [text] into the manual-paste field and taps `Restore`. The button
/// sits below the fold of the default 800x600 test viewport once the
/// camera-preview box, divider, and field are all laid out above it inside
/// the screen's `SingleChildScrollView`, so it must be scrolled into view
/// before `tap()` can hit-test it.
Future<void> _enterAndSubmit(WidgetTester tester, String text) async {
  await tester.enterText(_uuidField(), text);
  await tester.ensureVisible(_restoreButton());
  await tester.pumpAndSettle();
  await tester.tap(_restoreButton());
  await tester.pumpAndSettle();
}

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues(<String, Object>{});
  });

  testWidgets(
      'Restore via manual paste, valid format -> persisted, provider '
      'updated, and the screen pops (revealing the screen underneath)',
      (WidgetTester tester) async {
    const String id = '11111111-2222-4333-8444-555555555555';

    await _pumpPushedRestoreScreen(tester);
    expect(find.byType(RestoreProfileScreen), findsOneWidget);

    await _enterAndSubmit(tester, id);

    // The screen popped back to the route beneath it.
    expect(find.byType(RestoreProfileScreen), findsNothing);
    expect(find.text('base'), findsOneWidget);

    // Actually persisted.
    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readProfileId(), id);

    final SharedPreferences prefs = await SharedPreferences.getInstance();
    expect(prefs.getString(_profileIdKey), id);
  });

  testWidgets(
      'Restore via manual paste, valid format, upper-case with surrounding '
      'whitespace -> still restored (pasted values are not always a '
      'pristine copy-paste)', (WidgetTester tester) async {
    const String canonical = '99999999-8888-4777-8666-555555555555';

    await _pumpPushedRestoreScreen(tester);

    await _enterAndSubmit(tester, '  ${canonical.toUpperCase()}  ');

    expect(find.byType(RestoreProfileScreen), findsNothing);

    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readProfileId(), canonical);
  });

  testWidgets(
      'Restore, invalid format -> clear non-technical error shown, screen '
      'stays open, and nothing is persisted', (WidgetTester tester) async {
    await _pumpPushedRestoreScreen(tester);

    await _enterAndSubmit(tester, 'clearly not a uuid');

    // Still on the restore screen -- no navigation happened.
    expect(find.byType(RestoreProfileScreen), findsOneWidget);

    // A clear, non-technical error message is shown.
    expect(
      find.text(
        "That doesn't look like a valid code. Please check and try again.",
      ),
      findsOneWidget,
    );

    // Nothing persisted, no state change.
    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readProfileId(), isNull);
  });

  testWidgets(
      'Restore, invalid format then a valid retry -> the second attempt '
      'succeeds', (WidgetTester tester) async {
    const String id = '22222222-3333-4444-8555-666666666666';

    await _pumpPushedRestoreScreen(tester);

    await _enterAndSubmit(tester, 'nope');
    expect(
      find.text(
        "That doesn't look like a valid code. Please check and try again.",
      ),
      findsOneWidget,
    );
    expect(find.byType(RestoreProfileScreen), findsOneWidget);

    await _enterAndSubmit(tester, id);

    expect(find.byType(RestoreProfileScreen), findsNothing);

    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readProfileId(), id);
  });

  // The QR-scan "glue code" -- `_RestoreProfileScreenState._onBarcodeDetected`
  // -- extracts `capture.barcodes.first.rawValue` and forwards it into the
  // exact same `_attemptRestore` the manual-paste path above calls. Unlike a
  // real scan, this doesn't need a camera or the platform channel at all:
  // `BarcodeCapture`/`Barcode` (from `package:mobile_scanner`) are plain Dart
  // data classes, so a synthetic one can be built directly and fed straight
  // into the `MobileScanner` widget's `onDetect` callback.
  group('QR-scan glue code (MobileScanner.onDetect)', () {
    testWidgets(
        'A detected barcode with a valid UUID v4 rawValue -> restored, '
        'persisted, and the screen pops -- exactly like a successful '
        'manual-paste restore', (WidgetTester tester) async {
      const String id = '33333333-4444-4555-8666-777777777777';

      await _pumpPushedRestoreScreen(tester);
      expect(find.byType(RestoreProfileScreen), findsOneWidget);

      final MobileScanner scanner =
          tester.widget<MobileScanner>(find.byType(MobileScanner));
      expect(scanner.onDetect, isNotNull);

      scanner.onDetect!(
        BarcodeCapture(barcodes: <Barcode>[const Barcode(rawValue: id)]),
      );
      await tester.pumpAndSettle();

      // Same outcome as a successful manual-paste restore: the screen popped
      // back to the route beneath it.
      expect(find.byType(RestoreProfileScreen), findsNothing);
      expect(find.text('base'), findsOneWidget);

      // Actually persisted.
      const LearnerProfileStore freshStore = LearnerProfileStore();
      expect(await freshStore.readProfileId(), id);

      final SharedPreferences prefs = await SharedPreferences.getInstance();
      expect(prefs.getString(_profileIdKey), id);
    });

    testWidgets(
        'A detected barcode with an unrelated real-world rawValue (e.g. a '
        "URL) -> rejected with the same 'invalid format' error the "
        'manual-paste path shows, proving both entry methods funnel through '
        'the identical validation', (WidgetTester tester) async {
      await _pumpPushedRestoreScreen(tester);

      final MobileScanner scanner =
          tester.widget<MobileScanner>(find.byType(MobileScanner));
      expect(scanner.onDetect, isNotNull);

      scanner.onDetect!(
        const BarcodeCapture(
          barcodes: <Barcode>[
            Barcode(rawValue: 'https://example.com'),
          ],
        ),
      );
      await tester.pumpAndSettle();

      // Still on the restore screen -- no navigation happened.
      expect(find.byType(RestoreProfileScreen), findsOneWidget);

      // The exact same non-technical error the manual-paste path shows.
      expect(
        find.text(
          "That doesn't look like a valid code. Please check and try again.",
        ),
        findsOneWidget,
      );

      // Nothing persisted, no state change.
      const LearnerProfileStore freshStore = LearnerProfileStore();
      expect(await freshStore.readProfileId(), isNull);
    });
  });
}
