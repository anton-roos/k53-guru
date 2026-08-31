import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/available_sitting.dart';
import '../api/k53_api_client.dart';
import 'providers.dart';

/// The only layer widgets are allowed to depend on for sitting data --
/// wraps [K53ApiClient] so no HTTP call ever appears in a widget's
/// `build()` method.
class SittingsRepository {
  const SittingsRepository(this._apiClient);

  final K53ApiClient _apiClient;

  Future<List<AvailableSitting>> getAvailableSittings() {
    return _apiClient.getAvailableSittings();
  }
}

final Provider<SittingsRepository> sittingsRepositoryProvider =
    Provider<SittingsRepository>((Ref ref) {
  return SittingsRepository(ref.watch(k53ApiClientProvider));
});
