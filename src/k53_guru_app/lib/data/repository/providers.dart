import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/k53_api_client.dart';

/// The single [K53ApiClient] instance shared by every repository.
final Provider<K53ApiClient> k53ApiClientProvider = Provider<K53ApiClient>((Ref ref) {
  final K53ApiClient client = K53ApiClient();
  ref.onDispose(client.dispose);
  return client;
});
