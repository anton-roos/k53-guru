// Verifies AttemptsRepository correctly delegates to K53ApiClient and
// returns the parsed domain models, mirroring the coverage
// k53_api_client_test.dart already gives the client itself -- but exercised
// through the repository layer, which (unlike SittingsRepository) had no
// test of its own before this file.

import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:k53_guru_app/data/api/api_config.dart';
import 'package:k53_guru_app/data/api/attempt_answer_submission.dart';
import 'package:k53_guru_app/data/api/k53_api_client.dart';
import 'package:k53_guru_app/data/repository/attempts_repository.dart';
import 'package:k53_guru_app/domain/attempt.dart';
import 'package:k53_guru_app/domain/attempt_mode.dart';
import 'package:k53_guru_app/domain/check_answer_result.dart';
import 'package:k53_guru_app/domain/graded_attempt_result.dart';
import 'package:k53_guru_app/domain/licence_code.dart';

void main() {
  group('AttemptsRepository.startAttempt', () {
    test('delegates to K53ApiClient.startAttempt and returns the parsed Attempt',
        () async {
      final http.Client mock = MockClient((http.Request request) async {
        expect(request.method, 'POST');
        expect(request.url.toString(), '${ApiConfig.baseUrl}/attempts');
        final Map<String, dynamic> body =
            jsonDecode(request.body) as Map<String, dynamic>;
        expect(body, <String, dynamic>{
          'learnerProfileId': 'lp-1',
          'testId': 7,
          'mode': 'Practice',
        });

        return http.Response(
          jsonEncode(<String, dynamic>{
            'id': 42,
            'code': 'Code1',
            'mode': 'Practice',
            'startedAt': '2026-08-29T10:00:00.000Z',
            'attemptQuestions': <dynamic>[],
          }),
          200,
        );
      });

      final AttemptsRepository repository =
          AttemptsRepository(K53ApiClient(httpClient: mock));
      final Attempt attempt = await repository.startAttempt(
        learnerProfileId: 'lp-1',
        testId: 7,
        mode: AttemptMode.practice,
      );

      expect(attempt.id, 42);
      expect(attempt.code, <LicenceCode>[LicenceCode.code1]);
      expect(attempt.mode, AttemptMode.practice);
      expect(attempt.attemptQuestions, isEmpty);
    });
  });

  group('AttemptsRepository.getAttempt', () {
    test('delegates to K53ApiClient.getAttempt and returns the parsed Attempt',
        () async {
      final http.Client mock = MockClient((http.Request request) async {
        expect(request.method, 'GET');
        expect(request.url.path, endsWith('/attempts/42'));
        expect(request.url.queryParameters['learnerProfileId'], 'lp-1');

        return http.Response(
          jsonEncode(<String, dynamic>{
            'id': 42,
            'code': 'Code1, Code2',
            'mode': 'Test',
            'startedAt': '2026-08-29T10:00:00.000Z',
            'attemptQuestions': <dynamic>[],
          }),
          200,
        );
      });

      final AttemptsRepository repository =
          AttemptsRepository(K53ApiClient(httpClient: mock));
      final Attempt attempt = await repository.getAttempt(
        attemptId: 42,
        learnerProfileId: 'lp-1',
      );

      expect(attempt.id, 42);
      expect(attempt.code, <LicenceCode>[LicenceCode.code1, LicenceCode.code2]);
      expect(attempt.mode, AttemptMode.test);
    });
  });

  group('AttemptsRepository.submitAttempt', () {
    test(
        'delegates to K53ApiClient.submitAttempt and returns the parsed '
        'GradedAttemptResult', () async {
      final http.Client mock = MockClient((http.Request request) async {
        expect(request.method, 'POST');
        expect(request.url.path, endsWith('/attempts/42/submit'));
        final Map<String, dynamic> body =
            jsonDecode(request.body) as Map<String, dynamic>;
        expect(body['attemptId'], 42);
        expect(body['learnerProfileId'], 'lp-1');
        expect(body['answers'], <Map<String, dynamic>>[
          <String, dynamic>{
            'attemptQuestionId': 1,
            'selectedAttemptAnswerOptionId': 10,
          },
        ]);

        return http.Response(
          jsonEncode(<String, dynamic>{
            'attemptId': 42,
            'passed': true,
            'codeResults': <Map<String, dynamic>>[
              <String, dynamic>{
                'code': 'Code1',
                'passed': true,
                'sectionResults': <Map<String, dynamic>>[
                  <String, dynamic>{
                    'section': 'Rules',
                    'correctCount': 25,
                    'passMark': 22,
                    'passed': true,
                  },
                ],
              },
            ],
          }),
          200,
        );
      });

      final AttemptsRepository repository =
          AttemptsRepository(K53ApiClient(httpClient: mock));
      final GradedAttemptResult result = await repository.submitAttempt(
        attemptId: 42,
        learnerProfileId: 'lp-1',
        answers: const <AttemptAnswerSubmission>[
          AttemptAnswerSubmission(
            attemptQuestionId: 1,
            selectedAttemptAnswerOptionId: 10,
          ),
        ],
      );

      expect(result.attemptId, 42);
      expect(result.passed, isTrue);
      expect(result.codeResults.single.code, LicenceCode.code1);
    });
  });

  group('AttemptsRepository.checkAnswer', () {
    test(
        'delegates to K53ApiClient.checkAnswer and returns the parsed '
        'CheckAnswerResult', () async {
      final http.Client mock = MockClient((http.Request request) async {
        expect(request.method, 'POST');
        expect(request.url.path, endsWith('/attempts/42/check-answer'));
        final Map<String, dynamic> body =
            jsonDecode(request.body) as Map<String, dynamic>;
        expect(body, <String, dynamic>{
          'attemptId': 42,
          'learnerProfileId': 'lp-1',
          'attemptQuestionId': 1,
          'selectedAttemptAnswerOptionId': 10,
        });

        return http.Response(
          jsonEncode(<String, dynamic>{
            'isCorrect': true,
            'correctAttemptAnswerOptionId': 10,
            'explanation': null,
          }),
          200,
        );
      });

      final AttemptsRepository repository =
          AttemptsRepository(K53ApiClient(httpClient: mock));
      final CheckAnswerResult result = await repository.checkAnswer(
        attemptId: 42,
        learnerProfileId: 'lp-1',
        attemptQuestionId: 1,
        selectedAttemptAnswerOptionId: 10,
      );

      expect(result.isCorrect, isTrue);
      expect(result.correctAttemptAnswerOptionId, 10);
      expect(result.explanation, isNull);
    });
  });
}
