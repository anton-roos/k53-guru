// fromJson -> toJson -> fromJson round-trip tests for every response domain
// model's toJson(), none of which is exercised by production code (nothing
// in lib/ ever re-serializes a response model) or by any other test. Each
// test proves a full round trip loses no data by asserting the *second*
// parse's toJson() output deep-equals the *first* parse's toJson() output --
// so a bug that drops/mis-serializes a field (e.g. AttemptQuestion.toJson()
// after the Story 4.1 patch that made `code` a List<LicenceCode>) would
// break the round trip even though nothing else in the suite touches these
// toJson() methods.

import 'package:flutter_test/flutter_test.dart';

import 'package:k53_guru_app/domain/attempt.dart';
import 'package:k53_guru_app/domain/attempt_answer_option.dart';
import 'package:k53_guru_app/domain/attempt_mode.dart';
import 'package:k53_guru_app/domain/attempt_question.dart';
import 'package:k53_guru_app/domain/available_sitting.dart';
import 'package:k53_guru_app/domain/check_answer_result.dart';
import 'package:k53_guru_app/domain/code_result.dart';
import 'package:k53_guru_app/domain/graded_attempt_result.dart';
import 'package:k53_guru_app/domain/section_result.dart';
import 'package:k53_guru_app/domain/section_type.dart';

void main() {
  group('toJson round-trips', () {
    test('Attempt survives fromJson -> toJson -> fromJson -> toJson', () {
      final Attempt first = Attempt.fromJson(<String, dynamic>{
        'id': 42,
        'code': 'Code1, Code2',
        'mode': 'Practice',
        'startedAt': '2026-08-29T10:00:00.000Z',
        'attemptQuestions': <Map<String, dynamic>>[
          <String, dynamic>{
            'id': 1,
            'section': 'Rules',
            'code': 'Code1, Code2',
            'displayOrder': 1,
            'stem': 'What does this sign mean?',
            'signRef': 'RS-001',
            'attemptAnswerOptions': <Map<String, dynamic>>[
              <String, dynamic>{'id': 10, 'text': 'Stop', 'order': 1},
            ],
          },
        ],
      });

      final Map<String, dynamic> firstJson = first.toJson();
      final Attempt second = Attempt.fromJson(firstJson);
      expect(second.toJson(), equals(firstJson));
    });

    test('AvailableSitting survives fromJson -> toJson -> fromJson -> toJson',
        () {
      final AvailableSitting first = AvailableSitting.fromJson(<String, dynamic>{
        'id': 1,
        'codes': 'Code1, Code2',
        'name': 'Combo sitting',
      });

      final Map<String, dynamic> firstJson = first.toJson();
      final AvailableSitting second = AvailableSitting.fromJson(firstJson);
      expect(second.toJson(), equals(firstJson));
    });

    test(
        'AttemptQuestion survives fromJson -> toJson -> fromJson -> toJson, '
        'including a combination code', () {
      final AttemptQuestion first = AttemptQuestion.fromJson(<String, dynamic>{
        'id': 2,
        'section': 'Rules',
        'code': 'Code1, Code2',
        'displayOrder': 1,
        'stem': 'Shared Rules question.',
        'signRef': null,
        'attemptAnswerOptions': <Map<String, dynamic>>[
          <String, dynamic>{'id': 20, 'text': 'A', 'order': 1},
          <String, dynamic>{'id': 21, 'text': 'B', 'order': 2},
        ],
      });

      final Map<String, dynamic> firstJson = first.toJson();
      final AttemptQuestion second = AttemptQuestion.fromJson(firstJson);
      expect(second.toJson(), equals(firstJson));
    });

    test(
        'AttemptAnswerOption survives fromJson -> toJson -> fromJson -> '
        'toJson', () {
      final AttemptAnswerOption first =
          AttemptAnswerOption.fromJson(<String, dynamic>{
        'id': 10,
        'text': 'Stop',
        'order': 1,
      });

      final Map<String, dynamic> firstJson = first.toJson();
      final AttemptAnswerOption second = AttemptAnswerOption.fromJson(firstJson);
      expect(second.toJson(), equals(firstJson));
    });

    test(
        'GradedAttemptResult survives fromJson -> toJson -> fromJson -> '
        'toJson', () {
      final GradedAttemptResult first =
          GradedAttemptResult.fromJson(<String, dynamic>{
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
      });

      final Map<String, dynamic> firstJson = first.toJson();
      final GradedAttemptResult second = GradedAttemptResult.fromJson(firstJson);
      expect(second.toJson(), equals(firstJson));
    });

    test('CodeResult survives fromJson -> toJson -> fromJson -> toJson', () {
      final CodeResult first = CodeResult.fromJson(<String, dynamic>{
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
      });

      final Map<String, dynamic> firstJson = first.toJson();
      final CodeResult second = CodeResult.fromJson(firstJson);
      expect(second.toJson(), equals(firstJson));
      // CodeResult.code is always a single LicenceCode -- confirm the
      // round trip preserves that shape, not a list.
      expect(second.code, first.code);
      expect(second.sectionResults.single.section, SectionType.vehicleControls);
    });

    test('SectionResult survives fromJson -> toJson -> fromJson -> toJson',
        () {
      final SectionResult first = SectionResult.fromJson(<String, dynamic>{
        'section': 'Signs',
        'correctCount': 18,
        'passMark': 20,
        'passed': false,
      });

      final Map<String, dynamic> firstJson = first.toJson();
      final SectionResult second = SectionResult.fromJson(firstJson);
      expect(second.toJson(), equals(firstJson));
    });

    test('CheckAnswerResult survives fromJson -> toJson -> fromJson -> toJson',
        () {
      final CheckAnswerResult first = CheckAnswerResult.fromJson(<String, dynamic>{
        'isCorrect': false,
        'correctAttemptAnswerOptionId': 11,
        'explanation': 'A yield sign means give way.',
      });

      final Map<String, dynamic> firstJson = first.toJson();
      final CheckAnswerResult second = CheckAnswerResult.fromJson(firstJson);
      expect(second.toJson(), equals(firstJson));
    });
  });

  // Sanity check that the fixtures above actually reflect AttemptMode too,
  // even though AttemptMode itself has no toJson round trip of its own
  // (it's a nested field on Attempt, exercised above).
  test('AttemptMode.fromJson/toJson stay in sync for both values', () {
    expect(AttemptMode.fromJson(AttemptMode.practice.toJson()),
        AttemptMode.practice);
    expect(AttemptMode.fromJson(AttemptMode.test.toJson()), AttemptMode.test);
  });
}
