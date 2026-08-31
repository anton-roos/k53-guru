// Verifies JSON (de)serialization round-trips for every DTO shape
// `K53ApiClient` speaks, using `package:http/testing.dart`'s `MockClient`
// (no mocking dependency beyond `http`, which this story already adds).

import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:k53_guru_app/data/api/api_config.dart';
import 'package:k53_guru_app/data/api/attempt_answer_submission.dart';
import 'package:k53_guru_app/data/api/k53_api_client.dart';
import 'package:k53_guru_app/data/api/k53_api_exception.dart';
import 'package:k53_guru_app/domain/attempt.dart';
import 'package:k53_guru_app/domain/attempt_mode.dart';
import 'package:k53_guru_app/domain/available_sitting.dart';
import 'package:k53_guru_app/domain/check_answer_result.dart';
import 'package:k53_guru_app/domain/graded_attempt_result.dart';
import 'package:k53_guru_app/domain/licence_code.dart';
import 'package:k53_guru_app/domain/section_type.dart';

void main() {
  group('GET /sittings', () {
    test('parses a list of AvailableSittingDto, including a combo code', () async {
      final http.Client mock = MockClient((http.Request request) async {
        expect(request.method, 'GET');
        expect(request.url.toString(), '${ApiConfig.baseUrl}/sittings');
        return http.Response(
          jsonEncode(<Map<String, dynamic>>[
            <String, dynamic>{'id': 1, 'codes': 'Code1', 'name': 'Code 1 sitting'},
            <String, dynamic>{'id': 2, 'codes': 'Code1, Code2', 'name': null},
          ]),
          200,
          headers: <String, String>{'content-type': 'application/json'},
        );
      });

      final K53ApiClient client = K53ApiClient(httpClient: mock);
      final List<AvailableSitting> sittings = await client.getAvailableSittings();

      expect(sittings, hasLength(2));
      expect(sittings[0].id, 1);
      expect(sittings[0].codes, <LicenceCode>[LicenceCode.code1]);
      expect(sittings[0].name, 'Code 1 sitting');
      expect(sittings[1].id, 2);
      expect(sittings[1].codes, <LicenceCode>[LicenceCode.code1, LicenceCode.code2]);
      expect(sittings[1].name, isNull);
    });

    test('throws K53ApiException on a non-2xx response', () async {
      final http.Client mock = MockClient((http.Request request) async {
        return http.Response('{"succeeded":false,"errors":["boom"]}', 500);
      });

      final K53ApiClient client = K53ApiClient(httpClient: mock);
      expect(client.getAvailableSittings(), throwsA(isA<K53ApiException>()));
    });
  });

  group('POST /attempts', () {
    test('sends camelCase request body and parses the returned AttemptDto', () async {
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
            'attemptQuestions': <Map<String, dynamic>>[
              <String, dynamic>{
                'id': 1,
                'section': 'Rules',
                'code': 'Code1',
                'displayOrder': 1,
                'stem': 'What does this sign mean?',
                'signRef': 'RS-001',
                'attemptAnswerOptions': <Map<String, dynamic>>[
                  <String, dynamic>{'id': 10, 'text': 'Stop', 'order': 1},
                  <String, dynamic>{'id': 11, 'text': 'Yield', 'order': 2},
                ],
              },
            ],
          }),
          200,
        );
      });

      final K53ApiClient client = K53ApiClient(httpClient: mock);
      final Attempt attempt = await client.startAttempt(
        learnerProfileId: 'lp-1',
        testId: 7,
        mode: AttemptMode.practice,
      );

      expect(attempt.id, 42);
      expect(attempt.code, <LicenceCode>[LicenceCode.code1]);
      expect(attempt.mode, AttemptMode.practice);
      expect(attempt.startedAt, DateTime.parse('2026-08-29T10:00:00.000Z'));
      expect(attempt.attemptQuestions, hasLength(1));

      final question = attempt.attemptQuestions.single;
      expect(question.id, 1);
      expect(question.section, SectionType.rules);
      expect(question.code, <LicenceCode>[LicenceCode.code1]);
      expect(question.displayOrder, 1);
      expect(question.stem, 'What does this sign mean?');
      expect(question.signRef, 'RS-001');
      expect(question.attemptAnswerOptions, hasLength(2));
      expect(question.attemptAnswerOptions[0].id, 10);
      expect(question.attemptAnswerOptions[0].text, 'Stop');
      expect(question.attemptAnswerOptions[0].order, 1);
    });

    test(
        'parses a combination sitting where a shared Rules/Signs '
        'AttemptQuestion.code carries the full combo value', () async {
      final http.Client mock = MockClient((http.Request request) async {
        return http.Response(
          jsonEncode(<String, dynamic>{
            'id': 43,
            'code': 'Code1, Code2',
            'mode': 'Practice',
            'startedAt': '2026-08-29T10:00:00.000Z',
            'attemptQuestions': <Map<String, dynamic>>[
              <String, dynamic>{
                'id': 2,
                'section': 'Rules',
                'code': 'Code1, Code2',
                'displayOrder': 1,
                'stem': 'Shared Rules question for both codes.',
                'signRef': null,
                'attemptAnswerOptions': <Map<String, dynamic>>[
                  <String, dynamic>{'id': 20, 'text': 'A', 'order': 1},
                  <String, dynamic>{'id': 21, 'text': 'B', 'order': 2},
                ],
              },
              <String, dynamic>{
                'id': 3,
                'section': 'VehicleControls',
                'code': 'Code2',
                'displayOrder': 2,
                'stem': 'VehicleControls question for Code2 only.',
                'signRef': null,
                'attemptAnswerOptions': <Map<String, dynamic>>[
                  <String, dynamic>{'id': 30, 'text': 'A', 'order': 1},
                  <String, dynamic>{'id': 31, 'text': 'B', 'order': 2},
                ],
              },
            ],
          }),
          200,
        );
      });

      final K53ApiClient client = K53ApiClient(httpClient: mock);
      final Attempt attempt = await client.startAttempt(
        learnerProfileId: 'lp-1',
        testId: 8,
        mode: AttemptMode.practice,
      );

      expect(attempt.code, <LicenceCode>[LicenceCode.code1, LicenceCode.code2]);
      expect(attempt.attemptQuestions, hasLength(2));
      expect(
        attempt.attemptQuestions[0].code,
        <LicenceCode>[LicenceCode.code1, LicenceCode.code2],
      );
      expect(
        attempt.attemptQuestions[1].code,
        <LicenceCode>[LicenceCode.code2],
      );
    });
  });

  group('GET /attempts/{id}', () {
    test('passes learnerProfileId as a query parameter', () async {
      final http.Client mock = MockClient((http.Request request) async {
        expect(request.method, 'GET');
        expect(request.url.path, endsWith('/attempts/42'));
        expect(request.url.queryParameters['learnerProfileId'], 'lp-1');

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

      final K53ApiClient client = K53ApiClient(httpClient: mock);
      final Attempt attempt =
          await client.getAttempt(attemptId: 42, learnerProfileId: 'lp-1');

      expect(attempt.id, 42);
      expect(attempt.attemptQuestions, isEmpty);
    });
  });

  group('POST /attempts/{id}/submit', () {
    test('sends the answers array and parses a combo GradedAttemptResultDto',
        () async {
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
            'passed': false,
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
              <String, dynamic>{
                'code': 'Code2',
                'passed': false,
                'sectionResults': <Map<String, dynamic>>[
                  <String, dynamic>{
                    'section': 'VehicleControls',
                    'correctCount': 5,
                    'passMark': 8,
                    'passed': false,
                  },
                ],
              },
            ],
          }),
          200,
        );
      });

      final K53ApiClient client = K53ApiClient(httpClient: mock);
      final GradedAttemptResult result = await client.submitAttempt(
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
      expect(result.passed, isFalse);
      expect(result.codeResults, hasLength(2));
      expect(result.codeResults[0].code, LicenceCode.code1);
      expect(result.codeResults[0].passed, isTrue);
      expect(result.codeResults[0].sectionResults.single.correctCount, 25);
      expect(result.codeResults[1].code, LicenceCode.code2);
      expect(result.codeResults[1].passed, isFalse);
    });
  });

  group('POST /attempts/{id}/check-answer', () {
    test('parses CheckAnswerResultDto', () async {
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
            'isCorrect': false,
            'correctAttemptAnswerOptionId': 11,
            'explanation': 'A yield sign means give way.',
          }),
          200,
        );
      });

      final K53ApiClient client = K53ApiClient(httpClient: mock);
      final CheckAnswerResult result = await client.checkAnswer(
        attemptId: 42,
        learnerProfileId: 'lp-1',
        attemptQuestionId: 1,
        selectedAttemptAnswerOptionId: 10,
      );

      expect(result.isCorrect, isFalse);
      expect(result.correctAttemptAnswerOptionId, 11);
      expect(result.explanation, 'A yield sign means give way.');
    });
  });
}
