/// Thrown by [K53ApiClient] when the backend responds with a non-2xx
/// status. Carries the raw status code and body (typically a
/// `ProblemDetails`/`Result` JSON envelope) for the caller to inspect.
class K53ApiException implements Exception {
  const K53ApiException({required this.statusCode, required this.body});

  final int statusCode;
  final String body;

  @override
  String toString() => 'K53ApiException(statusCode: $statusCode, body: $body)';
}
