// Verifies `main.dart`'s portrait-only orientation lock -- Story 4.2's
// "Always: Lock orientation to portrait only (`SystemChrome.
// setPreferredOrientations` with `portraitUp`/`portraitDown`) at app
// startup, before `runApp`" boundary, which previously had zero automated
// coverage: nothing invoked `main()` or mocked the platform channel it
// calls, so a regression dropping/reordering the call, or passing the
// wrong orientation list, would go completely undetected.
//
// `SystemChrome.setPreferredOrientations` sends a
// `SystemChrome.setPreferredOrientations` method call (with the requested
// orientations serialized via `Enum.toString()`, e.g.
// `'DeviceOrientation.portraitUp'`) over `SystemChannels.platform`, a JSON
// `MethodChannel`. Mocking that channel via
// `TestDefaultBinaryMessengerBinding` lets this test capture the exact
// call `main()` makes without needing any platform plugin.
//
// `main()` is invoked directly (rather than extracting the orientation
// call into a separately-testable function) since this story's tests are
// test-only changes and must not touch `lib/`. `main()` does go on to
// build the real `K53GuruApp` (via `runApp`, synchronously during this
// call thanks to `AutomatedTestWidgetsFlutterBinding.scheduleWarmUpFrame`).
// Since Story 4.3, `K53GuruApp` is also the first-run router: it reads the
// persisted learner profile id via `learnerProfileProvider`, which in this
// unmocked test environment fails to reach any `SharedPreferences`
// platform implementation -- `LearnerProfileStore` treats that failure as
// "no profile id" per its own contract, so the router settles on
// `StartLearningScreen` rather than `AppShell`, and no network request for
// the Practice tab's sittings ever fires. Either way, the orientation call
// this test cares about is awaited and captured *before* `runApp` even
// runs, and the test never awaits/pumps further, so which screen `main()`
// ends up building has no bearing on this test's outcome.
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:k53_guru_app/main.dart' as app;

void main() {
  testWidgets(
      'main() locks the app to portrait-only orientation before runApp',
      (WidgetTester tester) async {
    final List<MethodCall> platformCalls = <MethodCall>[];

    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(SystemChannels.platform, (
      MethodCall call,
    ) async {
      platformCalls.add(call);
      return null;
    });
    addTearDown(() {
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(SystemChannels.platform, null);
    });

    await app.main();

    final MethodCall orientationCall = platformCalls.singleWhere(
      (MethodCall call) =>
          call.method == 'SystemChrome.setPreferredOrientations',
      orElse: () => throw StateError(
        'main() never called SystemChrome.setPreferredOrientations',
      ),
    );

    expect(
      orientationCall.arguments,
      <String>['DeviceOrientation.portraitUp', 'DeviceOrientation.portraitDown'],
    );
  });
}
