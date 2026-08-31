// Smoke test for the app shell: the themed `MaterialApp` launches the
// sittings proof screen and renders its data once the (overridden, network
// -free) sittings future resolves.

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:k53_guru_app/domain/available_sitting.dart';
import 'package:k53_guru_app/main.dart';
import 'package:k53_guru_app/presentation/sittings/sittings_list_provider.dart';

void main() {
  testWidgets('renders the sittings list screen inside the themed app shell',
      (WidgetTester tester) async {
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

    // First frame: the future hasn't resolved yet.
    expect(find.text('Available Sittings'), findsOneWidget);

    await tester.pumpAndSettle();

    // Empty-state text once the (stubbed) sittings future resolves.
    expect(find.text('No sittings available.'), findsOneWidget);
  });
}
