import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/repository/sittings_repository.dart';
import '../../domain/available_sitting.dart';

/// Loads the published sittings available to start, via
/// [SittingsRepository]. This is the only place the proof screen touches
/// data -- the widget only ever reads this provider, never the repository
/// or API client directly.
final FutureProvider<List<AvailableSitting>> availableSittingsProvider =
    FutureProvider<List<AvailableSitting>>((Ref ref) {
  final SittingsRepository repository = ref.watch(sittingsRepositoryProvider);
  return repository.getAvailableSittings();
});
