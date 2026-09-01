// Story 4.7 (accessibility floor) -- pins down Semantics coverage across
// every currently-existing screen:
//   - every interactive control exposes a non-empty accessible label
//     (`find.bySemanticsLabel(...)`, per the spec's own suggested API) --
//     for the vast majority of this app's controls that label comes from a
//     visible `Text` descendant, which Flutter's framework already turns
//     into that control's accessible label/name without any extra
//     `Semantics` wrapping needed.
//   - decorative icons (the `chevron_right` on "Change code", the `copy`
//     icon on "Copy UUID") carry no `semanticLabel` of their own, so they
//     are not separately/redundantly announced alongside their control's
//     real label.
//   - a focus-order spot check on one representative screen
//     (`LicenceCodeSelectionScreen`) confirming traversal order matches the
//     visual top-to-bottom layout -- a full per-screen traversal audit is
//     unnecessary because every current screen lays its controls out in a
//     single top-to-bottom `Column`/`ListView` with no custom
//     `SemanticsSortKey`, so Flutter's default traversal order already
//     matches paint order everywhere; this is a sanity check, not an
//     exhaustive one.

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

  group('StartLearningScreen', () {
    testWidgets(
        'both interactive controls expose a non-empty accessible label',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const StartLearningScreen()));
      await tester.pumpAndSettle();

      expect(find.bySemanticsLabel('Start learning'), findsOneWidget);
      expect(find.bySemanticsLabel('Restore profile'), findsOneWidget);
    });
  });

  group('RestoreProfileScreen', () {
    testWidgets(
        'the Restore action has a non-empty accessible label, and the '
        'manual-entry field has an adjacent visible text label',
        (WidgetTester tester) async {
      await tester.pumpWidget(
        _wrap(
          RestoreProfileScreen(
            scannerController: MobileScannerController(autoStart: false),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.bySemanticsLabel('Restore'), findsOneWidget);
      // The manual-entry `TextField` has no `labelText` of its own, but sits
      // directly beneath this visible heading -- satisfying "a visible Text
      // label already satisfies this" per the spec, so no extra Semantics
      // wrapping was added around it.
      expect(find.text('Paste your profile UUID'), findsOneWidget);
    });
  });

  group('LicenceCodeSelectionScreen', () {
    testWidgets('every option card exposes a non-empty accessible label',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const LicenceCodeSelectionScreen()));
      await tester.pumpAndSettle();

      expect(find.bySemanticsLabel('Code 1'), findsOneWidget);
      expect(find.bySemanticsLabel('Code 2'), findsOneWidget);
      expect(find.bySemanticsLabel('Code 3'), findsOneWidget);
    });

    testWidgets(
        'focus-order spot check: traversal order (headline, subtitle, '
        'Code 1, Code 2, Code 3) matches the visual top-to-bottom layout',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const LicenceCodeSelectionScreen()));
      await tester.pumpAndSettle();

      final List<String> expectedTopToBottom = <String>[
        'Which licence are you studying for?',
        "Pick one -- we'll show you exactly what you need.",
        'Code 1',
        'Code 2',
        'Code 3',
      ];

      final List<double> tops = expectedTopToBottom
          .map((String label) => tester.getTopLeft(find.text(label)).dy)
          .toList();

      for (int i = 1; i < tops.length; i++) {
        expect(
          tops[i],
          greaterThan(tops[i - 1]),
          reason:
              '"${expectedTopToBottom[i]}" must be positioned (and so '
              'traversed, since this screen uses plain top-to-bottom Column '
              'layout with no custom SemanticsSortKey) below '
              '"${expectedTopToBottom[i - 1]}"',
        );
      }
    });
  });

  group('ProfileScreen', () {
    setUp(() {
      SharedPreferences.setMockInitialValues(<String, Object>{
        'learner_profile_id': _profileId,
        'learner_licence_code': 'Code1',
      });
    });

    testWidgets(
        'every interactive control exposes a non-empty accessible label',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const ProfileScreen()));
      await tester.pumpAndSettle();

      expect(find.bySemanticsLabel('Copy UUID'), findsOneWidget);
      // `ListTile`/`SwitchListTile` merge their title+subtitle Text
      // descendants into a single semantics node (title, subtitle joined by
      // a newline), so these two use a `RegExp` rather than an exact-string
      // match -- the label is still genuinely non-empty and still carries
      // the control's own name, just alongside its subtitle text.
      expect(find.bySemanticsLabel(RegExp('Change code')), findsOneWidget);
      // The theme control is explicitly wrapped in `Semantics(label: 'Theme'
      // , ...)`, plus each segment carries its own visible Light/Dark label.
      expect(find.bySemanticsLabel('Theme'), findsOneWidget);
      expect(find.bySemanticsLabel('Light'), findsOneWidget);
      expect(find.bySemanticsLabel('Dark'), findsOneWidget);
      expect(
        find.bySemanticsLabel(RegExp('Read questions aloud')),
        findsOneWidget,
      );
      // The QR code image carries its own explicit semantics label so a
      // screen reader announces it meaningfully rather than skipping an
      // unlabeled image or reading raw pixel data.
      expect(
        find.bySemanticsLabel('QR code encoding your profile UUID'),
        findsOneWidget,
      );
    });

    testWidgets(
        'the "Change code" row\'s trailing chevron icon is purely decorative '
        '-- it carries no semanticLabel of its own, so it is not announced '
        'as a separate, redundant element alongside the row\'s real label',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const ProfileScreen()));
      await tester.pumpAndSettle();

      final Icon chevron = tester.widget<Icon>(
        find.descendant(
          of: find.byType(ListTile),
          matching: find.byIcon(Icons.chevron_right),
        ),
      );
      expect(chevron.semanticLabel, isNull);
    });

    testWidgets(
        'the "Copy UUID" button\'s copy icon is purely decorative -- it '
        'carries no semanticLabel of its own; the button\'s real accessible '
        'label comes from its visible "Copy UUID" text',
        (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const ProfileScreen()));
      await tester.pumpAndSettle();

      final Icon copyIcon = tester.widget<Icon>(
        find.descendant(
          of: find.widgetWithText(OutlinedButton, 'Copy UUID'),
          matching: find.byIcon(Icons.copy),
        ),
      );
      expect(copyIcon.semanticLabel, isNull);
    });

    testWidgets(
        'Recalibrate/Start fresh dialog actions expose non-empty accessible '
        'labels', (WidgetTester tester) async {
      await tester.pumpWidget(_wrap(const ProfileScreen()));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Change code'));
      await tester.pumpAndSettle();

      expect(find.bySemanticsLabel('Recalibrate'), findsOneWidget);
      expect(find.bySemanticsLabel('Start fresh'), findsOneWidget);
    });
  });

  group('AppShell', () {
    testWidgets(
        'every NavigationBar destination exposes a non-empty accessible '
        'label, and their icons carry no separate/redundant semanticLabel',
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

      final NavigationBar navBar =
          tester.widget<NavigationBar>(find.byType(NavigationBar));
      for (final NavigationDestination destination
          in navBar.destinations.cast<NavigationDestination>()) {
        // `NavigationDestination` composes its semantics node from more
        // than just the visible label (e.g. Material's own "Tab n of m"
        // selection-state text), so this matches on the label being
        // *present* within the node's accessible name (`RegExp`) rather
        // than requiring an exact match.
        expect(
          find.descendant(
            of: find.byType(NavigationBar),
            matching: find.bySemanticsLabel(RegExp(destination.label)),
          ),
          findsWidgets,
          reason: '"${destination.label}" destination must expose a '
              'non-empty accessible label',
        );

        final Icon icon = destination.icon as Icon;
        expect(
          icon.semanticLabel,
          isNull,
          reason: '"${destination.label}" destination\'s icon must not '
              'carry its own semanticLabel -- the destination\'s visible '
              'text label is the single accessible name, not a duplicate',
        );
      }
    });
  });
}
