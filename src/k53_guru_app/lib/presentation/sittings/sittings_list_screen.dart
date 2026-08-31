import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/available_sitting.dart';
import '../../domain/licence_code.dart';
import '../../theme/app_colors_extension.dart';
import '../../theme/app_spacing.dart';
import '../../theme/app_typography.dart';
import 'sittings_list_provider.dart';

/// Minimal proof screen for the layered architecture: widget ->
/// [availableSittingsProvider] -> `SittingsRepository` -> `K53ApiClient` ->
/// HTTP. Intentionally plain, unstyled beyond the shared theme -- real UI
/// is Epic 5/6's job.
class SittingsListScreen extends ConsumerWidget {
  const SittingsListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<List<AvailableSitting>> sittingsAsync =
        ref.watch(availableSittingsProvider);
    final ColorScheme colorScheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Available Sittings')),
      body: sittingsAsync.when(
        data: (List<AvailableSitting> sittings) => _SittingsList(sittings: sittings),
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (Object error, StackTrace stackTrace) => Center(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.space16),
            child: Text(
              'Failed to load sittings: $error',
              style: AppTypography.body.copyWith(color: colorScheme.error),
              textAlign: TextAlign.center,
            ),
          ),
        ),
      ),
    );
  }
}

class _SittingsList extends StatelessWidget {
  const _SittingsList({required this.sittings});

  final List<AvailableSitting> sittings;

  @override
  Widget build(BuildContext context) {
    if (sittings.isEmpty) {
      return Center(
        child: Text('No sittings available.', style: AppTypography.body),
      );
    }

    final palette = context.appColors;

    return ListView.separated(
      padding: const EdgeInsets.all(AppSpacing.space16),
      itemCount: sittings.length,
      separatorBuilder: (BuildContext context, int index) =>
          const SizedBox(height: AppSpacing.space12),
      itemBuilder: (BuildContext context, int index) {
        final AvailableSitting sitting = sittings[index];
        final String codes = sitting.codes.isEmpty
            ? '--'
            : sitting.codes
                .map((LicenceCode c) => c.toJson().replaceAll('Code', 'Code '))
                .join(' + ');

        return Card(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.space16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  sitting.name ?? 'Sitting #${sitting.id}',
                  style: AppTypography.option.copyWith(color: palette.ink),
                ),
                const SizedBox(height: AppSpacing.space4),
                Text(
                  codes,
                  style: AppTypography.label.copyWith(color: palette.muted),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}
