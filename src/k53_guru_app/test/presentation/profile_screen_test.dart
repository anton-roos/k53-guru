// Covers spec-4-4's I/O & Edge-Case Matrix rows "View Profile tab" and
// "Copy UUID": the UUID is shown and copyable, the exact save-your-progress
// microcopy from EXPERIENCE.md is present, and a QR code encoding the raw
// UUID string is rendered.
//
// Also covers spec-4-5's "Change code from Profile" matrix row: tapping
// `Change code`, confirming `Recalibrate` or `Start fresh`, and picking a
// new code replaces the old one and updates the displayed value.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/data/local/learner_profile_store.dart';
import 'package:k53_guru_app/domain/licence_code.dart';
import 'package:k53_guru_app/presentation/onboarding/licence_code_selection_screen.dart';
import 'package:k53_guru_app/presentation/profile/profile_screen.dart';
import 'package:k53_guru_app/theme/app_theme.dart';

const String _profileId = '11111111-2222-4333-8444-555555555555';

/// The rendered pixel bytes of the [QrPainter] actually wired to the
/// [QrImageView] found by [finder] -- i.e. the one built and painted inside
/// `profile_screen.dart`'s widget tree, not an independently-constructed
/// stand-in.
///
/// `QrImageView`'s own `data`/`_qrCode` fields are private with no public
/// getter (re-verified directly against the installed `qr_flutter` 4.1.0
/// package source: `lib/src/qr_image_view.dart` stores the constructor's
/// `data` argument in a field named `_data`), so it can't be read back
/// directly off the widget. What IS public is the `QrPainter` instance that
/// `_QrImageViewState` hands to the `CustomPaint` it builds -- `QrPainter`
/// exposes a public `toImageData()` that rasterises exactly what was/would be
/// painted on screen, which is a genuine, non-circumventable way to compare
/// "what this widget actually renders" against "what an independently-built
/// `QrPainter` for a known string renders", using only `qr_flutter`'s public
/// API.
Future<Uint8List> _renderedQrBytes(WidgetTester tester, Finder finder) async {
  final CustomPaint customPaint = tester.widget<CustomPaint>(
    find.descendant(of: finder, matching: find.byType(CustomPaint)),
  );
  final QrPainter painter = customPaint.painter! as QrPainter;
  final ByteData bytes = (await painter.toImageData(200))!;
  return bytes.buffer.asUint8List();
}

Widget _wrap(Widget child) {
  return ProviderScope(
    child: MaterialApp(theme: AppTheme.light(), home: child),
  );
}

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_profile_id': _profileId,
      'learner_licence_code': 'Code1',
    });
  });

  testWidgets(
      'Profile exists -> UUID shown selectable/copyable, save-your-progress '
      'microcopy present, and a QR code encoding it is rendered',
      (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const ProfileScreen()));
    await tester.pumpAndSettle();

    // The UUID itself, shown as selectable text (copyable per Flutter's own
    // `SelectableText` long-press-to-select/copy affordance).
    expect(find.byType(SelectableText), findsOneWidget);
    expect(
      tester.widget<SelectableText>(find.byType(SelectableText)).data,
      _profileId,
    );

    // The exact save-your-progress microcopy from EXPERIENCE.md's
    // "Identity & Profile" section -- must match verbatim, not paraphrased.
    expect(
      find.text(
        'To save your progress, copy this UUID to import your results in '
        'another app',
      ),
      findsOneWidget,
    );

    // A QR code widget is rendered, carrying the semantics label
    // `profile_screen.dart` sets specifically for it (rather than some
    // other, unrelated `QrImageView` accidentally matching).
    final Finder qrCode = find.byType(QrImageView);
    expect(qrCode, findsOneWidget);
    expect(
      tester.widget<QrImageView>(qrCode).semanticsLabel,
      'QR code encoding your profile UUID',
    );

    // Genuine data-correctness check: the QR code's *actual rendered
    // pixels* -- not just its presence or an unrelated label -- must match
    // what an independently-constructed `QrPainter` renders for the exact
    // same profile UUID, using `qr_flutter`'s public `QrPainter`/
    // `toImageData()` API (see `_renderedQrBytes`'s doc comment for why this
    // is the genuine public API for this, given `QrImageView.data` itself
    // is private with no getter). A regression that encoded the wrong value
    // while leaving the semantics label untouched would be caught by this.
    await tester.runAsync(() async {
      final Uint8List actualBytes = await _renderedQrBytes(tester, qrCode);

      final QrPainter expectedPainter = QrPainter(
        data: _profileId,
        version: QrVersions.auto,
        // `QrImageView`'s own default is `gapless: true`, unlike
        // `QrPainter`'s raw constructor default of `false` -- match it here
        // so this comparison isn't tripped up by an unrelated rendering
        // difference between the two independently-constructed painters.
        gapless: true,
      );
      final Uint8List expectedBytes =
          (await expectedPainter.toImageData(200))!.buffer.asUint8List();

      expect(
        actualBytes,
        equals(expectedBytes),
        reason: 'the QR code actually rendered by ProfileScreen must encode '
            'exactly $_profileId',
      );

      // Differential proxy on top of the direct check above: a QR code
      // built for a different profile id must render different pixels,
      // proving `data:` is genuinely wired to the prop rather than
      // coincidentally matching a hardcoded value.
      final QrPainter differentPainter = QrPainter(
        data: 'ffffffff-eeee-4ddd-8ccc-bbbbbbbbbbbb',
        version: QrVersions.auto,
        gapless: true,
      );
      final Uint8List differentBytes =
          (await differentPainter.toImageData(200))!.buffer.asUint8List();

      expect(
        actualBytes,
        isNot(equals(differentBytes)),
        reason: 'a QR code for a different profile id must render '
            'different pixels than the one actually shown',
      );
    });
  });

  testWidgets('Tap the copy action -> UUID is copied to the system clipboard',
      (WidgetTester tester) async {
    String? copiedText;
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
      SystemChannels.platform,
      (MethodCall call) async {
        if (call.method == 'Clipboard.setData') {
          copiedText = (call.arguments as Map<Object?, Object?>)['text']
              as String?;
        }
        return null;
      },
    );
    addTearDown(() {
      tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
        SystemChannels.platform,
        null,
      );
    });

    await tester.pumpWidget(_wrap(const ProfileScreen()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Copy UUID'));
    await tester.pumpAndSettle();

    expect(
      copiedText,
      _profileId,
      reason: 'tapping the copy action must copy the exact profile UUID to '
          'the system clipboard',
    );
  });

  testWidgets('Change code row shows the current code',
      (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const ProfileScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Change code'), findsOneWidget);
    expect(find.text('Code 1'), findsOneWidget);
  });

  testWidgets(
      'Tap Change code -> confirmation dialog offers exactly Recalibrate '
      'and Start fresh', (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const ProfileScreen()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Change code'));
    await tester.pumpAndSettle();

    expect(find.byType(AlertDialog), findsOneWidget);
    expect(find.text('Recalibrate'), findsOneWidget);
    expect(find.text('Start fresh'), findsOneWidget);
  });

  testWidgets(
      'Change code, confirm Recalibrate, pick a new code -> the new code '
      "replaces the old one and the row's displayed value updates",
      (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const ProfileScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Code 1'), findsOneWidget);

    await tester.tap(find.text('Change code'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Recalibrate'));
    await tester.pumpAndSettle();

    // The dialog is gone and LicenceCodeSelectionScreen is now showing,
    // pushed on top of Profile.
    expect(find.byType(AlertDialog), findsNothing);
    expect(find.byType(LicenceCodeSelectionScreen), findsOneWidget);

    await tester.tap(find.text('Code 3'));
    await tester.pumpAndSettle();

    // Popped back to Profile, and the new code is both persisted and
    // reflected in the row's displayed value.
    expect(find.byType(LicenceCodeSelectionScreen), findsNothing);
    expect(find.byType(ProfileScreen), findsOneWidget);
    expect(find.text('Code 3'), findsOneWidget);
    expect(find.text('Code 1'), findsNothing);

    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readLicenceCode(), LicenceCode.code3);
  });

  testWidgets(
      'Change code, confirm Start fresh, pick a new code -> the new code '
      'replaces the old one (both dialog choices lead to the same '
      're-selection flow)', (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const ProfileScreen()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Change code'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Start fresh'));
    await tester.pumpAndSettle();

    expect(find.byType(LicenceCodeSelectionScreen), findsOneWidget);

    await tester.tap(find.text('Code 2'));
    await tester.pumpAndSettle();

    expect(find.byType(LicenceCodeSelectionScreen), findsNothing);
    expect(find.text('Code 2'), findsOneWidget);

    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readLicenceCode(), LicenceCode.code2);
  });

  testWidgets(
      'Rapid double-tap on Change code -> only one AlertDialog is shown',
      (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const ProfileScreen()));
    await tester.pumpAndSettle();

    // Two taps back-to-back with no pump in between, simulating a rapid
    // double-tap: the second tap must be swallowed by `_isChangingCode`
    // before a second `AlertDialog` can be stacked on the Navigator.
    await tester.tap(find.text('Change code'));
    await tester.tap(find.text('Change code'));
    await tester.pumpAndSettle();

    expect(find.byType(AlertDialog), findsOneWidget);
  });

  testWidgets(
      'Tap Change code then dismiss the dialog without choosing -> the '
      'current code is left untouched, no code picker shown',
      (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const ProfileScreen()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Change code'));
    await tester.pumpAndSettle();
    expect(find.byType(AlertDialog), findsOneWidget);

    // Tap outside the dialog to dismiss it without choosing either option.
    await tester.tapAt(const Offset(10, 10));
    await tester.pumpAndSettle();

    expect(find.byType(AlertDialog), findsNothing);
    expect(find.byType(LicenceCodeSelectionScreen), findsNothing);
    expect(find.text('Code 1'), findsOneWidget);

    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readLicenceCode(), LicenceCode.code1);
  });
}
