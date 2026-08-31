// Smoke test for the app shell: the themed `MaterialApp` launches the
// sittings proof screen and renders its data once the (overridden, network
// -free) sittings future resolves.
//
// Since Story 4.3, `K53GuruApp` is also the first-run router: it only
// launches `AppShell` (and therefore this screen) once
// `learnerProfileProvider` resolves to an already-persisted profile id.
// `SharedPreferences.setMockInitialValues` simulates a returning learner so
// this smoke test reaches `AppShell` -- Story 4.3's own tests
// (`test/presentation/start_learning_screen_test.dart`) cover the
// first-run/returning-learner routing decision itself.

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/domain/available_sitting.dart';
import 'package:k53_guru_app/main.dart';
import 'package:k53_guru_app/presentation/sittings/sittings_list_provider.dart';

void main() {
  testWidgets('renders the sittings list screen inside the themed app shell',
      (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_profile_id': '11111111-2222-4333-8444-555555555555',
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
        child: const K53GuruApp(),
      ),
    );

    // Let the (mocked, effectively-instant) persisted-profile-id read
    // resolve so the router settles on `AppShell` before asserting.
    await tester.pump();

    expect(find.text('Available Sittings'), findsOneWidget);

    await tester.pumpAndSettle();

    // Empty-state text once the (stubbed) sittings future resolves.
    expect(find.text('No sittings available.'), findsOneWidget);
  });
}
