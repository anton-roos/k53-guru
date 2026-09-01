// Covers spec-4-5's I/O & Edge-Case Matrix rows that concern
// `LicenceCodeSelectionScreen` itself:
//  - "Fresh profile, no code chosen" -> the screen renders (already pinned
//    end-to-end via the real router in `k53_guru_app_router_test.dart` and
//    `start_learning_screen_test.dart`; this file focuses on the screen's
//    own content/behaviour).
//  - "Pick a code, first time" -> tapping an option persists it and
//    proceeds.
//  - Reused (pushed) for the Profile tab's `Change code` flow -> selecting
//    a code pops back to reveal the screen underneath.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/data/local/learner_profile_store.dart';
import 'package:k53_guru_app/domain/licence_code.dart';
import 'package:k53_guru_app/presentation/onboarding/licence_code_provider.dart';
import 'package:k53_guru_app/presentation/onboarding/licence_code_selection_screen.dart';
import 'package:k53_guru_app/theme/app_theme.dart';

const String _licenceCodeKey = 'learner_licence_code';

Widget _wrap(Widget child) {
  return ProviderScope(
    child: MaterialApp(theme: AppTheme.light(), home: child),
  );
}

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues(<String, Object>{});
  });

  testWidgets(
      'Renders three equally-weighted, tappable options -- Code 1, Code 2, '
      'Code 3 -- and nothing else to decide', (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const LicenceCodeSelectionScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Code 1'), findsOneWidget);
    expect(find.text('Code 2'), findsOneWidget);
    expect(find.text('Code 3'), findsOneWidget);

    // Every option is a real tap target, each at least the accessibility
    // floor's 48px minimum tap target.
    for (final String label in <String>['Code 1', 'Code 2', 'Code 3']) {
      final Finder inkWell = find.ancestor(
        of: find.text(label),
        matching: find.byType(InkWell),
      );
      expect(inkWell, findsOneWidget);
      expect(tester.getSize(inkWell).height, greaterThanOrEqualTo(48));
    }
  });

  testWidgets(
      'Pick a code, first time -> tapping Code 2 persists it immediately '
      '-- no separate confirm step', (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const LicenceCodeSelectionScreen()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Code 2'));
    await tester.pumpAndSettle();

    // Actually persisted through the real store, not just held in memory.
    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readLicenceCode(), LicenceCode.code2);

    final SharedPreferences prefs = await SharedPreferences.getInstance();
    expect(prefs.getString(_licenceCodeKey), 'Code2');
  });

  Future<void> expectTapPersists(
    WidgetTester tester,
    String label,
    LicenceCode expected,
  ) async {
    SharedPreferences.setMockInitialValues(<String, Object>{});
    final ProviderContainer container = ProviderContainer();
    addTearDown(container.dispose);

    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const LicenceCodeSelectionScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text(label));
    await tester.pumpAndSettle();

    expect(container.read(licenceCodeProvider).value, expected);
  }

  testWidgets('Tapping Code 1 updates licenceCodeProvider state to code1',
      (WidgetTester tester) async {
    await expectTapPersists(tester, 'Code 1', LicenceCode.code1);
  });

  testWidgets('Tapping Code 2 updates licenceCodeProvider state to code2',
      (WidgetTester tester) async {
    await expectTapPersists(tester, 'Code 2', LicenceCode.code2);
  });

  testWidgets('Tapping Code 3 updates licenceCodeProvider state to code3',
      (WidgetTester tester) async {
    await expectTapPersists(tester, 'Code 3', LicenceCode.code3);
  });

  testWidgets(
      'Pushed on top of another screen (the Change code flow) -> selecting '
      'a code pops back to reveal the screen underneath',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(
        child: MaterialApp(
          theme: AppTheme.light(),
          home: Builder(
            builder: (BuildContext context) => Scaffold(
              body: Center(
                child: ElevatedButton(
                  onPressed: () => Navigator.of(context).push(
                    MaterialPageRoute<void>(
                      builder: (_) => const LicenceCodeSelectionScreen(),
                    ),
                  ),
                  child: const Text('Open selection'),
                ),
              ),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Open selection'));
    await tester.pumpAndSettle();
    expect(find.byType(LicenceCodeSelectionScreen), findsOneWidget);

    await tester.tap(find.text('Code 3'));
    await tester.pumpAndSettle();

    // Popped back to the screen underneath, and the new code was still
    // persisted before popping.
    expect(find.byType(LicenceCodeSelectionScreen), findsNothing);
    expect(find.text('Open selection'), findsOneWidget);

    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readLicenceCode(), LicenceCode.code3);
  });

  testWidgets(
      'Rapid double-tap across two different options only persists the '
      'first one tapped', (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const LicenceCodeSelectionScreen()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Code 1'));
    // A single pump (not pumpAndSettle) inspects the mid-flight moment
    // before the async persist/pop resolves -- the options must already be
    // disabled by then.
    await tester.pump();

    await tester.tap(find.text('Code 2'));
    await tester.pumpAndSettle();

    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(
      await freshStore.readLicenceCode(),
      LicenceCode.code1,
      reason: 'the second tap (Code 2), landing while a selection is '
          'already in flight, must not overwrite the first',
    );
  });
}
