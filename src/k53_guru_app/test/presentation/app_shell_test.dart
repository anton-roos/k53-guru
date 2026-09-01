// Verifies Story 4.2's three-tab bottom-nav shell:
//  - exactly 3 destinations, in the fixed Practice/Test/Profile order.
//  - tapping a destination switches the displayed content.
//  - previously-displayed tabs' widgets remain in the tree (`IndexedStack`
//    state preservation), not disposed/rebuilt on switch.
//  - each destination's rendered tap target is >= 48px tall.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/domain/available_sitting.dart';
import 'package:k53_guru_app/domain/licence_code.dart';
import 'package:k53_guru_app/presentation/shell/app_shell.dart';
import 'package:k53_guru_app/presentation/sittings/sittings_list_provider.dart';
import 'package:k53_guru_app/theme/app_theme.dart';

// The Profile tab is now Story 4.4's real `ProfileScreen`, which reads
// `learnerProfileProvider` (backed by `SharedPreferences`) as soon as
// `AppShell`'s `IndexedStack` builds it -- every tab is built immediately,
// regardless of which one is selected. Without a mock store, that read
// never resolves in the test environment, leaving the Profile tab's
// `CircularProgressIndicator` animating forever and `pumpAndSettle` timing
// out; a fixed profile id lets it resolve immediately, matching a real
// returning learner.
const String _mockProfileId = '11111111-2222-4333-8444-555555555555';

Widget _wrap(Widget child) {
  return ProviderScope(
    overrides: [
      availableSittingsProvider.overrideWith(
        (Ref ref) => Future<List<AvailableSitting>>.value(
          const <AvailableSitting>[],
        ),
      ),
    ],
    child: MaterialApp(theme: AppTheme.light(), home: child),
  );
}

/// Finds the given [label]'s text specifically inside the `NavigationBar`
/// (as opposed to an identically-labelled placeholder screen kept alive
/// underneath by the `IndexedStack`).
Finder _destinationLabel(String label) => find.descendant(
      of: find.byType(NavigationBar),
      matching: find.text(label),
    );

/// Finds the given [text] specifically inside the `IndexedStack` (i.e. tab
/// *content*, as opposed to the `NavigationBar`'s own destination labels,
/// which are always on-screen regardless of the selected tab and would
/// otherwise be mistaken for a placeholder screen with the same label).
///
/// `skipOffstage: false` is required on *both* `find.descendant` and the
/// inner `find.text` -- they each have their own independent skipOffstage
/// flag -- because `IndexedStack` keeps every tab's subtree mounted in the
/// element tree at all times (that's the whole point -- state preservation)
/// but only paints/hit-tests the selected one, and skipOffstage's default of
/// `true` treats the non-selected ones as absent rather than merely
/// not-currently-visible.
Finder _tabContent(String text) => find.descendant(
      of: find.byType(IndexedStack),
      matching: find.text(text, skipOffstage: false),
      skipOffstage: false,
    );

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_profile_id': _mockProfileId,
    });
  });

  testWidgets('exposes exactly 3 destinations in Practice/Test/Profile order',
      (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const AppShell()));

    final NavigationBar navBar =
        tester.widget<NavigationBar>(find.byType(NavigationBar));

    expect(navBar.destinations.length, 3);
    expect(
      navBar.destinations
          .map((Widget d) => (d as NavigationDestination).label)
          .toList(),
      <String>['Practice', 'Test', 'Profile'],
    );
  });

  testWidgets('tab switching updates the displayed content',
      (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const AppShell()));
    await tester.pumpAndSettle();

    // Practice tab (index 0) is selected first: Story 4.1's proof screen is
    // actually hit-testable/on-screen; the other tabs' placeholders exist
    // in the tree (IndexedStack builds all of them) but aren't.
    expect(_tabContent('Available Sittings').hitTestable(), findsOneWidget);
    expect(_tabContent('Test').hitTestable(), findsNothing);
    expect(_tabContent('Profile').hitTestable(), findsNothing);

    await tester.tap(_destinationLabel('Test'));
    await tester.pumpAndSettle();

    expect(_tabContent('Available Sittings').hitTestable(), findsNothing);
    expect(_tabContent('Test').hitTestable(), findsOneWidget);
    expect(_tabContent('Profile').hitTestable(), findsNothing);

    await tester.tap(_destinationLabel('Profile'));
    await tester.pumpAndSettle();

    expect(_tabContent('Available Sittings').hitTestable(), findsNothing);
    expect(_tabContent('Test').hitTestable(), findsNothing);
    expect(_tabContent('Profile').hitTestable(), findsOneWidget);
  });

  testWidgets(
      'previously-displayed tabs remain in the tree across switches '
      '(IndexedStack preserves state)', (WidgetTester tester) async {
    // A plain "is the static text still findable" check can't distinguish
    // genuine `IndexedStack` state preservation from a fresh rebuild that
    // just happens to reproduce the same static text -- none of the three
    // tabs has any real mutable Flutter `State` by default. So this test
    // overrides `availableSittingsProvider` with enough items to make
    // Practice's `ListView` genuinely scrollable, scrolls it, switches
    // tabs, and asserts the *scroll position* (real, mutable `State`)
    // survives -- something only possible if the subtree was truly kept
    // alive, never rebuilt.
    final List<AvailableSitting> manySittings = List<AvailableSitting>.generate(
      20,
      (int i) => AvailableSitting(
        id: i,
        codes: const <LicenceCode>[LicenceCode.code1],
        name: 'Sitting #$i',
      ),
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          availableSittingsProvider.overrideWith(
            (Ref ref) => Future<List<AvailableSitting>>.value(manySittings),
          ),
        ],
        child: MaterialApp(theme: AppTheme.light(), home: const AppShell()),
      ),
    );
    await tester.pumpAndSettle();

    // Practice's content exists at start.
    expect(_tabContent('Available Sittings'), findsOneWidget);

    // Scroll Practice's list to a non-zero offset. `find.byType(Scrollable)`
    // finds exactly Practice's list here: it's the only `Scrollable` in the
    // tree at all, and (per the default `skipOffstage: true`) only the
    // currently-selected `IndexedStack` child is onstage regardless.
    final Finder scrollable = find.byType(Scrollable);
    expect(scrollable, findsOneWidget);

    await tester.drag(scrollable, const Offset(0, -600));
    await tester.pumpAndSettle();

    final double scrolledOffset =
        tester.state<ScrollableState>(scrollable).position.pixels;
    expect(
      scrolledOffset,
      greaterThan(0),
      reason: 'the list must actually have scrolled for this test to prove '
          'anything -- widen the item count/drag distance if this fails',
    );

    // Switch to Test, then Profile -- Practice's Scaffold/AppBar should
    // still be present in the tree (just not hit-testable), proving the
    // IndexedStack kept it alive rather than disposing/rebuilding it.
    await tester.tap(_destinationLabel('Test'));
    await tester.pumpAndSettle();
    expect(_tabContent('Available Sittings'), findsOneWidget);

    await tester.tap(_destinationLabel('Profile'));
    await tester.pumpAndSettle();
    expect(_tabContent('Available Sittings'), findsOneWidget);
    // The Test placeholder built earlier is also still alive underneath.
    expect(_tabContent('Test'), findsOneWidget);

    // The IndexedStack itself keeps exactly 3 children mounted throughout.
    final IndexedStack stack =
        tester.widget<IndexedStack>(find.byType(IndexedStack));
    expect(stack.children.length, 3);
    expect(stack.index, 2);

    // Switch back to Practice: its `ScrollPosition` -- real, mutable
    // `State`, not static text -- must be exactly where it was left. A
    // fresh rebuild (i.e. `IndexedStack` failing to preserve state) would
    // have reset this to 0.
    await tester.tap(_destinationLabel('Practice'));
    await tester.pumpAndSettle();

    final double restoredOffset = tester
        .state<ScrollableState>(find.byType(Scrollable))
        .position
        .pixels;
    expect(
      restoredOffset,
      scrolledOffset,
      reason: 'IndexedStack should keep Practice\'s ScrollPosition alive -- '
          'a changed value here means the tab was rebuilt instead of '
          'preserved.',
    );
  });

  testWidgets('each destination renders a tap target >= 48px tall',
      (WidgetTester tester) async {
    await tester.pumpWidget(_wrap(const AppShell()));
    await tester.pumpAndSettle();

    // Each destination is laid out inside nested `Expanded` widgets (an
    // outer one from `NavigationBar`'s own layout, plus an inner one from
    // `NavigationDestination`'s internal icon/label arrangement) that both
    // span the full height of the `NavigationBar` -- that height *is* the
    // destination's actual tap target, so any one of them measures it; take
    // the first match rather than asserting there's exactly one, since that
    // count is a Material implementation detail, not part of the contract.
    for (final String label in <String>['Practice', 'Test', 'Profile']) {
      final Finder destination = find.ancestor(
        of: _destinationLabel(label),
        matching: find.byType(Expanded),
      );

      expect(destination, findsWidgets);

      final double height = tester.getSize(destination.first).height;
      expect(
        height,
        greaterThanOrEqualTo(48),
        reason: '"$label" destination tap target must be >= 48px tall',
      );
    }
  });
}
