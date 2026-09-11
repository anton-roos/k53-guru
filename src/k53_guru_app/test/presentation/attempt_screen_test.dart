// Proves the minimal interactive attempt flow end-to-end: tapping a sitting
// card in SittingsListScreen navigates to AttemptScreen, which starts a
// Practice-mode attempt, lets the learner answer a question with immediate
// feedback (checkAnswer), then submit for grading and land on
// AttemptResultScreen -- all through the real provider chain
// (k53ApiClientProvider is the only override), matching the wiring-test
// pattern sittings_list_screen_wiring_test.dart already established.

import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:k53_guru_app/data/api/k53_api_client.dart';
import 'package:k53_guru_app/data/repository/providers.dart';
import 'package:k53_guru_app/presentation/attempt/attempt_result_screen.dart';
import 'package:k53_guru_app/presentation/attempt/attempt_screen.dart';
import 'package:k53_guru_app/presentation/onboarding/learner_profile_provider.dart';
import 'package:k53_guru_app/presentation/sittings/sittings_list_screen.dart';

/// Resolves immediately to a fixed learner profile id -- this flow only
/// needs a non-null id to be present, not any of LearnerProfileNotifier's
/// own read/write/restore behaviour (already covered by its own tests).
class _FixedLearnerProfileNotifier extends LearnerProfileNotifier {
  @override
  Future<String?> build() async => 'lp-test-1';
}

void main() {
  testWidgets(
      'Tap a sitting -> start attempt -> check an answer -> finish -> see the result',
      (WidgetTester tester) async {
    final http.Client mock = MockClient((http.Request request) async {
      final String path = request.url.path;

      if (request.method == 'GET' && path.endsWith('/sittings')) {
        return http.Response(
          jsonEncode(<Map<String, dynamic>>[
            <String, dynamic>{'id': 1, 'codes': 'Code1', 'name': 'Code 1 Practice Test'},
          ]),
          200,
          headers: <String, String>{'content-type': 'application/json'},
        );
      }

      if (request.method == 'POST' && path.endsWith('/attempts')) {
        final Map<String, dynamic> body =
            jsonDecode(request.body) as Map<String, dynamic>;
        expect(body['testId'], 1);
        expect(body['mode'], 'Practice');
        return http.Response(
          jsonEncode(<String, dynamic>{
            'id': 99,
            'code': 'Code1',
            'mode': 'Practice',
            'startedAt': '2026-09-08T10:00:00.000Z',
            'attemptQuestions': <Map<String, dynamic>>[
              <String, dynamic>{
                'id': 501,
                'section': 'Signs',
                'code': 'Code1',
                'displayOrder': 0,
                'stem': 'What does this road sign mean?',
                'signRef': 'R1',
                'attemptAnswerOptions': <Map<String, dynamic>>[
                  <String, dynamic>{'id': 1001, 'text': 'Stop', 'order': 0},
                  <String, dynamic>{'id': 1002, 'text': 'Give Way / Yield', 'order': 1},
                ],
              },
            ],
          }),
          200,
          headers: <String, String>{'content-type': 'application/json'},
        );
      }

      if (request.method == 'POST' && path.endsWith('/check-answer')) {
        final Map<String, dynamic> body =
            jsonDecode(request.body) as Map<String, dynamic>;
        expect(body['attemptQuestionId'], 501);
        expect(body['selectedAttemptAnswerOptionId'], 1001);
        return http.Response(
          jsonEncode(<String, dynamic>{
            'isCorrect': true,
            'correctAttemptAnswerOptionId': 1001,
            'explanation': 'A Stop sign always requires a complete standstill.',
          }),
          200,
          headers: <String, String>{'content-type': 'application/json'},
        );
      }

      if (request.method == 'POST' && path.endsWith('/submit')) {
        final Map<String, dynamic> body =
            jsonDecode(request.body) as Map<String, dynamic>;
        expect(body['answers'], <dynamic>[
          <String, dynamic>{'attemptQuestionId': 501, 'selectedAttemptAnswerOptionId': 1001},
        ]);
        return http.Response(
          jsonEncode(<String, dynamic>{
            'attemptId': 99,
            'passed': true,
            'codeResults': <Map<String, dynamic>>[
              <String, dynamic>{
                'code': 'Code1',
                'passed': true,
                'sectionResults': <Map<String, dynamic>>[
                  <String, dynamic>{
                    'section': 'Signs',
                    'correctCount': 1,
                    'passMark': 1,
                    'passed': true,
                  },
                ],
              },
            ],
          }),
          200,
          headers: <String, String>{'content-type': 'application/json'},
        );
      }

      fail('Unexpected request: ${request.method} ${request.url}');
    });

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          k53ApiClientProvider.overrideWithValue(K53ApiClient(httpClient: mock)),
          learnerProfileProvider.overrideWith(() => _FixedLearnerProfileNotifier()),
        ],
        child: const MaterialApp(home: SittingsListScreen()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Code 1 Practice Test'), findsOneWidget);

    // Tap the sitting card -> navigates to AttemptScreen, which starts the
    // attempt and shows its first (only) question.
    await tester.tap(find.text('Code 1 Practice Test'));
    await tester.pumpAndSettle();

    expect(find.byType(AttemptScreen), findsOneWidget);
    expect(find.text('What does this road sign mean?'), findsOneWidget);
    expect(find.text('Sign: R1'), findsOneWidget);
    expect(find.text('Road Signs · Question 1 of 1'), findsOneWidget);

    // Select "Stop", check it -> immediate correct feedback.
    await tester.tap(find.text('Stop'));
    await tester.pump();
    await tester.tap(find.text('Check answer'));
    await tester.pumpAndSettle();

    expect(find.text('Correct'), findsOneWidget);
    expect(
      find.text('A Stop sign always requires a complete standstill.'),
      findsOneWidget,
    );

    // Last question -> the primary action is now "Finish", not "Next question".
    expect(find.text('Finish'), findsOneWidget);
    expect(find.text('Next question'), findsNothing);

    await tester.tap(find.text('Finish'));
    await tester.pumpAndSettle();

    // Submitted -> lands on the result screen with the real graded outcome.
    expect(find.byType(AttemptResultScreen), findsOneWidget);
    expect(find.text('Passed!'), findsOneWidget);
    expect(find.text('Code 1'), findsOneWidget);
    expect(find.textContaining('1 correct (need 1)'), findsOneWidget);
  });
}
