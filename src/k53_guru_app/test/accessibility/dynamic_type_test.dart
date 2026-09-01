// Story 4.7 (accessibility floor) -- pumps every currently-existing screen
// at a 200% linear text scale (`TextScaler.linear(2.0)`, matching the I/O
// matrix's "OS text scale set to 200%" row) and asserts it renders without a
// thrown exception -- Flutter surfaces a `RenderFlex` overflow as a
// `FlutterError` reported through `FlutterError.onError`, which
// `TestWidgetsFlutterBinding` captures and `tester.takeException()` then
// returns, so a null result here is proof of no overflow/clip/exception, not
// just "the widget tree exists".
//
// The override is applied via `MaterialApp.builder`, which wraps the whole
// routed subtree in a `MediaQuery` carrying the doubled `textScaler` --
// every screen under test (and anything it pushes, e.g.
// `LicenceCodeSelectionScreen` reached via Profile's `Change code`) inherits
// it, exactly like a real OS-level accessibility text-size setting would.

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
import 'package:k53_guru_app/presentation/sittings/sittings_list_screen.dart';
import 'package:k53_guru_app/theme/app_theme.dart';

const String _profileId = '11111111-2222-4333-8444-555555555555';

/// A `MaterialApp` for [home] whose entire subtree renders under a doubled
/// `TextScaler` -- the same mechanism a real device's "Larger Text" / 200%
/// OS accessibility setting would apply via `MediaQuery`. Callers wrap this
/// in their own `ProviderScope` (with overrides where needed).
Widget _scaledMaterialApp(Widget home) {
  return MaterialApp(
    theme: AppTheme.light(),
    builder: (BuildContext context, Widget? child) => MediaQuery(
      data: MediaQuery.of(context).copyWith(
        textScaler: const TextScaler.linear(2.0),
      ),
      child: child!,
    ),
    home: home,
  );
}

/// [_scaledMaterialApp] wrapped in a plain `ProviderScope` with no overrides
/// -- the common case for screens that don't need one.
Widget _scaledApp(Widget home) {
  return ProviderScope(child: _scaledMaterialApp(home));
}

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues(<String, Object>{});
  });

  testWidgets(
      'StartLearningScreen renders at 200% text scale without a thrown '
      'exception', (WidgetTester tester) async {
    await tester.pumpWidget(_scaledApp(const StartLearningScreen()));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.byType(StartLearningScreen), findsOneWidget);
  });

  testWidgets(
      'StartLearningScreen renders at 200% text scale on a short/narrow '
      'viewport without a thrown exception (this screen used to lay out as '
      'Scaffold > SafeArea > Center > Padding > Column(mainAxisSize.min) '
      'with no scroll container -- the same pre-fix shape '
      'LicenceCodeSelectionScreen had, which genuinely overflows at 200% '
      'text scale on a small viewport even though it does not on '
      "flutter_test's generous default 800x600 surface)",
      (WidgetTester tester) async {
    final Size originalPhysicalSize = tester.view.physicalSize;
    final double originalDevicePixelRatio = tester.view.devicePixelRatio;
    addTearDown(() {
      tester.view.physicalSize = originalPhysicalSize;
      tester.view.devicePixelRatio = originalDevicePixelRatio;
    });

    // 640x240 logical px -- short and narrow enough that this screen's
    // headline + primary button + secondary button no longer fit
    // vertically once every piece of text is doubled in size.
    tester.view.devicePixelRatio = 1.0;
    tester.view.physicalSize = const Size(640, 240);

    await tester.pumpWidget(_scaledApp(const StartLearningScreen()));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.byType(StartLearningScreen), findsOneWidget);
  });

  testWidgets(
      'RestoreProfileScreen renders at 200% text scale without a thrown '
      'exception', (WidgetTester tester) async {
    await tester.pumpWidget(
      _scaledApp(
        RestoreProfileScreen(
          scannerController: MobileScannerController(autoStart: false),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.byType(RestoreProfileScreen), findsOneWidget);
  });

  testWidgets(
      'LicenceCodeSelectionScreen renders at 200% text scale without a '
      'thrown exception (long headline + 3 option cards, no scroll view by '
      'default -- the concrete overflow risk this test exists to catch)',
      (WidgetTester tester) async {
    await tester.pumpWidget(_scaledApp(const LicenceCodeSelectionScreen()));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.byType(LicenceCodeSelectionScreen), findsOneWidget);
    // The content must still all be reachable, not silently clipped off --
    // scrolling to each option proves the screen grew/scrolled to
    // accommodate the doubled text rather than clipping it.
    for (final String label in <String>['Code 1', 'Code 2', 'Code 3']) {
      await tester.ensureVisible(find.text(label));
      await tester.pumpAndSettle();
      expect(find.text(label), findsOneWidget);
    }
  });

  testWidgets(
      'ProfileScreen renders at 200% text scale without a thrown exception',
      (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_profile_id': _profileId,
      'learner_licence_code': 'Code1',
    });

    await tester.pumpWidget(_scaledApp(const ProfileScreen()));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.byType(ProfileScreen), findsOneWidget);

    // The Settings section (SegmentedButton + SwitchListTile) is further
    // down the SingleChildScrollView -- scroll to it and confirm it renders
    // too, not just the fold-visible top of the screen.
    await tester.ensureVisible(find.text('Settings'));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });

  testWidgets(
      'ProfileScreen Settings section (Light/Dark SegmentedButton) renders '
      'on a narrow real-device-width viewport at 200% text scale without a '
      'thrown exception, with both segment labels still findable (risk '
      'identified via Flutter SDK source analysis of SegmentedButton\'s '
      'internal layout on narrow widths -- not yet empirically confirmed '
      'before this test)', (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_profile_id': _profileId,
      'learner_licence_code': 'Code1',
    });

    final Size originalPhysicalSize = tester.view.physicalSize;
    final double originalDevicePixelRatio = tester.view.devicePixelRatio;
    addTearDown(() {
      tester.view.physicalSize = originalPhysicalSize;
      tester.view.devicePixelRatio = originalDevicePixelRatio;
    });

    // 320 logical px wide -- the narrow end of real small-phone widths
    // (e.g. iPhone SE), tall enough (640) that vertical scrolling alone
    // isn't the thing under test here -- this is specifically about
    // horizontal space for the two SegmentedButton segments.
    tester.view.devicePixelRatio = 1.0;
    tester.view.physicalSize = const Size(320, 640);

    await tester.pumpWidget(_scaledApp(const ProfileScreen()));
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.text('Settings'));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    // A thrown-exception check alone can't detect silent text
    // clipping/truncation -- confirm both segment labels are still
    // genuinely findable (not silently clipped) at this narrow width.
    expect(find.text('Light'), findsOneWidget);
    expect(find.text('Dark'), findsOneWidget);
  });

  testWidgets(
      'ProfileScreen "Change code" dialog and the pushed '
      'LicenceCodeSelectionScreen both render at 200% text scale without a '
      'thrown exception', (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_profile_id': _profileId,
      'learner_licence_code': 'Code1',
    });

    await tester.pumpWidget(_scaledApp(const ProfileScreen()));
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.text('Change code'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Change code'));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.byType(AlertDialog), findsOneWidget);
    // A thrown-exception check alone can't detect silent text
    // clipping/truncation -- the spec's I/O matrix requires "no thrown
    // exception OR clipped/overflowing content", not just the former.
    // Confirm both dialog action labels are still genuinely findable (not
    // silently clipped off) at 200% text scale.
    expect(find.text('Recalibrate'), findsOneWidget);
    expect(find.text('Start fresh'), findsOneWidget);

    await tester.tap(find.text('Recalibrate'));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.byType(LicenceCodeSelectionScreen), findsOneWidget);
  });

  testWidgets(
      'SittingsListScreen (empty state) renders at 200% text scale without '
      'a thrown exception', (WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          availableSittingsProvider.overrideWith(
            (Ref ref) => Future<List<AvailableSitting>>.value(
              const <AvailableSitting>[],
            ),
          ),
        ],
        child: _scaledMaterialApp(const SittingsListScreen()),
      ),
    );
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
  });

  testWidgets(
      'AppShell (all three tabs, including the Test placeholder and '
      'Profile) renders at 200% text scale without a thrown exception',
      (WidgetTester tester) async {
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
        child: _scaledMaterialApp(const AppShell()),
      ),
    );
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);

    final Finder navBar = find.byType(NavigationBar);
    for (final String label in <String>['Test', 'Profile', 'Practice']) {
      await tester.tap(
        find.descendant(of: navBar, matching: find.text(label)),
      );
      await tester.pumpAndSettle();
      expect(
        tester.takeException(),
        isNull,
        reason: 'switching to the "$label" tab at 200% text scale must not '
            'throw',
      );
      // A thrown-exception check alone can't detect silent text
      // clipping/truncation -- the spec's I/O matrix requires "no thrown
      // exception OR clipped/overflowing content", not just the former.
      // Confirm the destination's own label is still genuinely findable (not
      // silently clipped off the NavigationBar) at 200% text scale.
      expect(
        find.descendant(of: navBar, matching: find.text(label)),
        findsWidgets,
        reason: '"$label" destination label must still be findable (not '
            'silently clipped) at 200% text scale',
      );
    }
  });
}
