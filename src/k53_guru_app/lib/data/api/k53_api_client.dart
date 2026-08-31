import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../domain/attempt.dart';
import '../../domain/attempt_mode.dart';
import '../../domain/available_sitting.dart';
import '../../domain/check_answer_result.dart';
import '../../domain/graded_attempt_result.dart';
import 'api_config.dart';
import 'attempt_answer_submission.dart';
import 'k53_api_exception.dart';

/// Typed client for the Epic 3 learner-facing `/api/v1` surface. Wraps
/// every existing endpoint; this is the only place in the app that issues
/// an HTTP call -- repositories call this, widgets call repositories.
class K53ApiClient {
  K53ApiClient({http.Client? httpClient}) : _httpClient = httpClient ?? http.Client();

  final http.Client _httpClient;

  static const Map<String, String> _jsonHeaders = <String, String>{
    'Content-Type': 'application/json',
  };

  /// `GET /api/v1/sittings` -- published sittings available to start.
  Future<List<AvailableSitting>> getAvailableSittings() async {
    final http.Response response = await _httpClient.get(_uri('/sittings'));
    _throwIfNotOk(response);
    final List<dynamic> body = jsonDecode(response.body) as List<dynamic>;
    return body
        .map((dynamic e) => AvailableSitting.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  /// `POST /api/v1/attempts` -- starts a new attempt for [testId] (an
  /// `AvailableSitting.id`) under [learnerProfileId], in [mode].
  Future<Attempt> startAttempt({
    required String learnerProfileId,
    required int testId,
    required AttemptMode mode,
  }) async {
    final http.Response response = await _httpClient.post(
      _uri('/attempts'),
      headers: _jsonHeaders,
      body: jsonEncode(<String, dynamic>{
        'learnerProfileId': learnerProfileId,
        'testId': testId,
        'mode': mode.toJson(),
      }),
    );
    _throwIfNotOk(response);
    return Attempt.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  /// `GET /api/v1/attempts/{id}` -- resumes an existing attempt; returns
  /// the identical snapshotted question order.
  Future<Attempt> getAttempt({
    required int attemptId,
    required String learnerProfileId,
  }) async {
    final http.Response response = await _httpClient.get(
      _uri('/attempts/$attemptId', <String, String>{
        'learnerProfileId': learnerProfileId,
      }),
    );
    _throwIfNotOk(response);
    return Attempt.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  /// `POST /api/v1/attempts/{id}/submit` -- submits [answers] for grading.
  /// [clientSubmittedAt] is diagnostic only; elapsed time is computed
  /// server-side.
  Future<GradedAttemptResult> submitAttempt({
    required int attemptId,
    required String learnerProfileId,
    required List<AttemptAnswerSubmission> answers,
    DateTime? clientSubmittedAt,
  }) async {
    final http.Response response = await _httpClient.post(
      _uri('/attempts/$attemptId/submit'),
      headers: _jsonHeaders,
      body: jsonEncode(<String, dynamic>{
        'attemptId': attemptId,
        'learnerProfileId': learnerProfileId,
        'answers':
            answers.map((AttemptAnswerSubmission a) => a.toJson()).toList(),
        'clientSubmittedAt': clientSubmittedAt?.toIso8601String(),
      }),
    );
    _throwIfNotOk(response);
    return GradedAttemptResult.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  /// `POST /api/v1/attempts/{id}/check-answer` -- Practice-mode-only
  /// inline correctness check for a single question.
  Future<CheckAnswerResult> checkAnswer({
    required int attemptId,
    required String learnerProfileId,
    required int attemptQuestionId,
    required int selectedAttemptAnswerOptionId,
  }) async {
    final http.Response response = await _httpClient.post(
      _uri('/attempts/$attemptId/check-answer'),
      headers: _jsonHeaders,
      body: jsonEncode(<String, dynamic>{
        'attemptId': attemptId,
        'learnerProfileId': learnerProfileId,
        'attemptQuestionId': attemptQuestionId,
        'selectedAttemptAnswerOptionId': selectedAttemptAnswerOptionId,
      }),
    );
    _throwIfNotOk(response);
    return CheckAnswerResult.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  Uri _uri(String path, [Map<String, String>? queryParameters]) {
    return Uri.parse('${ApiConfig.baseUrl}$path')
        .replace(queryParameters: queryParameters);
  }

  void _throwIfNotOk(http.Response response) {
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw K53ApiException(statusCode: response.statusCode, body: response.body);
    }
  }

  /// Releases the underlying HTTP client's resources.
  void dispose() => _httpClient.close();
}
