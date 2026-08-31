/// Backend API configuration.
///
/// Per this story's scope, there is exactly ONE reachable target -- no
/// dev/staging/prod split, no Android-emulator `10.0.2.2` handling.
/// Environment configuration is deferred to a later story.
///
/// Defaults to the backend's local HTTP launch profile
/// (`src/K53Guru/src/Server.UI/Properties/launchSettings.json`).
class ApiConfig {
  const ApiConfig._();

  static const String baseUrl = 'http://localhost:5056/api/v1';
}
