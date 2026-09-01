// Story 4.7 (accessibility floor) -- pins down the 48px minimum tap target
// (`AppSpacing.minTapTarget`) across:
//   - the theme-level fix itself (`outlinedButtonTheme`/`textButtonTheme`
//     minimumSize), so a future edit to `app_theme.dart` can't silently
//     shrink it back down, and
//   - every currently-existing screen's interactive controls named in the
//     spec's I/O matrix: buttons, dialog actions, list rows, the switch, the
//     segmented control, the licence-code option cards, and the nav bar
//     destinations.
//
// Screens/controls already covered by an existing, more targeted test file
// (`licence_code_selection_screen_test.dart`'s option-card check,
// `app_shell_test.dart`'s nav-bar-destination check) are re-asserted here too
// -- briefly -- so this dedicated accessibility suite is a genuine single
// source of truth for the floor, not reliant on remembering those other
// files also happen to cover it.
//
// Two distinct, non-overlapping things are actually being verified by the
// two groups of tests below, despite both nominally being about the same
// 48px floor -- confirmed empirically (temporarily deleting
// `app_theme.dart`'s `outlinedButtonTheme`/`textButtonTheme` entirely and
// re-running this file):
//   - The three "real rendered button" per-screen tests (`ProfileScreen`'s
//     "Copy UUID", `StartLearningScreen`'s "Restore profile", and the
//     Recalibrate/Start fresh dialog `TextButton`s) kept passing even with
//     the theme change fully removed. That's not a gap in those tests --
//     Flutter's own `OutlinedButton`/`TextButton` already wrap their tap
//     area in an `_InputPadding` that inflates the hit-test box to
//     `kMinInteractiveDimension` (48.0) whenever
//     `MaterialTapTargetSize.padded` is in effect (Material's own default),
//     entirely independent of the button's own `minimumSize` style. So
//     these three tests are a genuine, correct floor check of what's
//     actually rendered today, and they would still catch a real regression
//     at one of those call sites -- e.g. a future
//     `style: OutlinedButton.styleFrom(tapTargetSize:
//     MaterialTapTargetSize.shrinkWrap)` override, which WOULD defeat
//     Flutter's automatic padding. What they do *not* do is guard
//     `app_theme.dart`'s own `minimumSize` configuration specifically --
//     removing it doesn't make any of these three tests fail.
//   - The three isolated theme-property tests just below (reading
//     `AppTheme.light().outlinedButtonTheme.style.minimumSize` etc. directly
//     off `ThemeData`, with no widget rendered at all) are what actually
//     catch that removal -- they're the single source of truth for "is the
//     app's own theme-level minimumSize configuration still there", a
//     property Flutter's automatic padding happens to make redundant for
//     today's standard buttons, but which remains real, distinct insurance
//     (e.g. against a future non-standard button variant, or a Flutter
//     default change, that doesn't get the same automatic inflation).
// Neither set of tests should be removed or weakened in favor of the other
// -- each one guards something the other one doesn't.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/domain/available_sitting.dart';
import 'package:k53_guru_app/presentation/onboarding/start_learning_screen.dart';
import 'package:k53_guru_app/presentation/onboarding/licence_code_selection_screen.dart';
import 'package:k53_guru_app/presentation/profile/profile_screen.dart';
import 'package:k53_guru_app/presentation/profile/restore_profile_screen.dart';
import 'package:k53_guru_app/presentation/shell/app_shell.dart';
import 'package:k53_guru_app/presentation/sittings/sittings_list_provider.dart';
import 'package:k53_guru_app/theme/app_spacing.dart';
import 'package:k53_guru_app/theme/app_theme.dart';

const String _profileId = '11111111-2222-4333-8444-555555555555';

Widget _wrap(Widget child) {
  return ProviderScope(
    child: MaterialApp(theme: AppTheme.light(), home: child),
  );
}

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues(<String, Object>{});
  });

  group('theme-level minimumSize (app_theme.dart)', () {
    test(
        'outlinedButtonTheme.style.minimumSize is at least '
        'AppSpacing.minTapTarget tall', () {
      final ButtonStyle? style = AppTheme.light().outlinedButtonTheme.style;
      expect(style, isNotNull);
      final Size? minimumSize = style!.minimumSize?.resolve(<WidgetState>{});
      expect(minimumSize, isNotNull);
      expect(minimumSize!.height, greaterThanOrEqualTo(AppSpacing.minTapTarget));
    });

    test(
        'textButtonTheme.style.minimumSize is at least AppSpacing.minTapTarget '
        'tall', () {
      final ButtonStyle? style = AppTheme.light().textButtonTheme.style;
      expect(style, isNotNull);
      final Size? minimumSize = style!.minimumSize?.resolve(<WidgetState>{});
      expect(minimumSize, isNotNull);
      expect(minimumSize!.height, greaterThanOrEqualTo(AppSpacing.minTapTarget));
    });

    test('dark() theme also carries the same themed button minimums', () {
      final ButtonStyle outlined = AppTheme.dark().outlinedButtonTheme.style!;
      final ButtonStyle text = AppTheme.dark().textButtonTheme.style!;
      expect(
        outlined.minimumSize?.resolve(<WidgetState>{})?.height,
        greaterThanOrEqualTo(AppSpacing.minTapTarget),
      );
      expect(
        text.minimumSize?.resolve(<WidgetState>{})?.height,
        greaterThanOrEqualTo(AppSpacing.minTapTarget),
      );
    });
  });

  group('ProfileScreen', () {
    setUp(() {
      SharedPreferences.setMockInitialValues(<String, Object>{
        'learner_profile_id': _profileId,
        'learner_licence_code': 'Code1',
      });
    });

    testWidgets('"Copy UUID" OutlinedButton renders >= 48px tall',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const ProfileScreen()));
      await tester.pumpAndSettle();

      final Finder button = find.widgetWithText(OutlinedButton, 'Copy UUID');
      expect(button, findsOneWidget);
      expect(tester.getSize(button).height, greaterThanOrEqualTo(48));
    });

    testWidgets('"Change code" ListTile row renders >= 48px tall',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const ProfileScreen()));
      await tester.pumpAndSettle();

      // `find.byType(ListTile)` alone would also match the `ListTile`
      // `SwitchListTile` builds internally for its own row -- disambiguate
      // by the visible "Change code" title text.
      final Finder tile = find.widgetWithText(ListTile, 'Change code');
      expect(tile, findsOneWidget);
      expect(tester.getSize(tile).height, greaterThanOrEqualTo(48));
    });

    testWidgets(
        'Recalibrate/Start fresh dialog TextButtons render >= 48px tall',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const ProfileScreen()));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Change code'));
      await tester.pumpAndSettle();

      final Finder recalibrate =
          find.widgetWithText(TextButton, 'Recalibrate');
      final Finder startFresh =
          find.widgetWithText(TextButton, 'Start fresh');
      expect(recalibrate, findsOneWidget);
      expect(startFresh, findsOneWidget);
      expect(tester.getSize(recalibrate).height, greaterThanOrEqualTo(48));
      expect(tester.getSize(startFresh).height, greaterThanOrEqualTo(48));
    });

    testWidgets('theme SegmentedButton control renders >= 48px tall',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const ProfileScreen()));
      await tester.pumpAndSettle();

      final Finder segmented =
          find.byKey(const Key('themeModeSegmentedButton'));
      expect(segmented, findsOneWidget);
      expect(tester.getSize(segmented).height, greaterThanOrEqualTo(48));
    });

    testWidgets('TTS SwitchListTile row renders >= 48px tall',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const ProfileScreen()));
      await tester.pumpAndSettle();

      final Finder switchTile = find.byKey(const Key('ttsEnabledSwitch'));
      expect(switchTile, findsOneWidget);
      expect(tester.getSize(switchTile).height, greaterThanOrEqualTo(48));
    });
  });

  group('StartLearningScreen', () {
    testWidgets('"Restore profile" TextButton renders >= 48px tall',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const StartLearningScreen()));
      await tester.pumpAndSettle();

      final Finder button =
          find.widgetWithText(TextButton, 'Restore profile');
      expect(button, findsOneWidget);
      expect(tester.getSize(button).height, greaterThanOrEqualTo(48));
    });

    testWidgets('"Start learning" ElevatedButton renders >= 48px tall',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const StartLearningScreen()));
      await tester.pumpAndSettle();

      final Finder button =
          find.widgetWithText(ElevatedButton, 'Start learning');
      expect(button, findsOneWidget);
      expect(tester.getSize(button).height, greaterThanOrEqualTo(48));
    });
  });

  group('RestoreProfileScreen', () {
    testWidgets('"Restore" ElevatedButton renders >= 48px tall',
        (WidgetTester tester) async {
      await tester.pumpWidget(
        _wrap(
          RestoreProfileScreen(
            scannerController: MobileScannerController(autoStart: false),
          ),
        ),
      );
      await tester.pumpAndSettle();

      final Finder button = find.widgetWithText(ElevatedButton, 'Restore');
      await tester.ensureVisible(button);
      await tester.pumpAndSettle();
      expect(tester.getSize(button).height, greaterThanOrEqualTo(48));
    });
  });

  group('LicenceCodeSelectionScreen', () {
    testWidgets('each option card renders >= 48px tall',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const LicenceCodeSelectionScreen()));
      await tester.pumpAndSettle();

      for (final String label in <String>['Code 1', 'Code 2', 'Code 3']) {
        final Finder inkWell = find.ancestor(
          of: find.text(label),
          matching: find.byType(InkWell),
        );
        expect(inkWell, findsOneWidget);
        expect(tester.getSize(inkWell).height, greaterThanOrEqualTo(48));
      }
    });
  });

  group('AppShell', () {
    testWidgets('each NavigationBar destination renders >= 48px tall',
        (WidgetTester tester) async {
      // A profile id must already be persisted -- otherwise the Profile
      // tab's `ProfileScreen` (built immediately by `AppShell`'s
      // `IndexedStack`, regardless of which tab is selected) is stuck
      // showing a perpetually-animating `CircularProgressIndicator`, which
      // hangs `pumpAndSettle` forever.
      SharedPreferences.setMockInitialValues(<String, Object>{
        'learner_profile_id': _profileId,
        'learner_licence_code': 'Code1',
      });

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            availableSittingsProvider.overrideWith(
              (Ref ref) => Future<List<AvailableSitting>>.value(
                const <AvailableSitting>[],
              ),
            ),
          ],
          child: MaterialApp(theme: AppTheme.light(), home: const AppShell()),
        ),
      );
      await tester.pumpAndSettle();

      final Finder navBar = find.byType(NavigationBar);
      for (final String label in <String>['Practice', 'Test', 'Profile']) {
        // Measuring `find.ancestor(matching: find.byType(Expanded))` here
        // (as an earlier version of this test did) is not a meaningful
        // check: `NavigationBar` composes each destination inside nested
        // `Expanded`s that are *always* stretched to the bar's own
        // configured height (`NavigationBar.height`, 80dp by default)
        // regardless of the destination's actual icon/label content --
        // confirmed directly against the installed Flutter SDK's
        // `navigation_bar.dart` source: each destination's
        // `CustomMultiChildLayout` sizes itself via the delegate's default
        // `getSize()`, which returns `constraints.biggest`, so it always
        // fills whatever height it's given. That assertion would pass
        // unconditionally for any `NavigationBar` content and can't catch a
        // regression in the destination's own tappable region. It is also
        // reaching into `Expanded` -- a Flutter-internal implementation
        // detail of `NavigationBar` two composition layers deep, not part
        // of its public contract, and free to change shape in a future
        // Flutter release.
        //
        // Measuring the destination's `SemanticsNode.rect` instead (the
        // same technique `semantics_audit_test.dart` already uses to find
        // these same labels) checks the actual bounds Flutter publishes to
        // assistive technology for this control -- the real, public
        // contract for "how big is this tappable thing", not an
        // implementation artifact. It was verified (via a scratch
        // experiment: temporarily passing `NavigationBar(height: 40)` in
        // `app_shell.dart`, confirming this exact assertion below failed
        // with the shrunk height, then reverting the experiment) that this
        // assertion genuinely fails when the destination's real tappable
        // region shrinks below the floor -- unlike the `Expanded`-ancestor
        // version, it isn't just incidentally correlated with the bar's
        // configured height, it measures the value that actually matters.
        final Finder destinationLabel = find.descendant(
          of: navBar,
          matching: find.bySemanticsLabel(RegExp(label)),
        );
        expect(destinationLabel, findsOneWidget);

        final Rect rect = tester.getSemantics(destinationLabel).rect;
        expect(
          rect.height,
          greaterThanOrEqualTo(48),
          reason: '"$label" destination tap target must be >= 48px tall',
        );
      }
    });
  });
}
