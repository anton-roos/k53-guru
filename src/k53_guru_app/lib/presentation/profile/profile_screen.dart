import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../../theme/app_spacing.dart';
import '../onboarding/learner_profile_provider.dart';

/// The exact save-your-progress microcopy from EXPERIENCE.md's "Identity &
/// Profile" section -- reused verbatim rather than re-typed inline so there
/// is exactly one place this string can drift from the spec.
const String kSaveProgressMicrocopy =
    'To save your progress, copy this UUID to import your results in '
    'another app';

/// The Profile tab's real content (replacing Story 4.2's placeholder):
/// the learner's UUID as selectable/copyable text, a copy-to-clipboard
/// action, the save-your-progress note, and a QR code encoding the raw
/// UUID string -- so it can be scanned by `restore_profile_screen.dart` on
/// another device/install.
///
/// [learnerProfileProvider] is expected to already hold a non-null id by
/// the time this screen is ever shown -- it lives inside `AppShell`, which
/// the root router in `main.dart` only ever displays once the provider has
/// resolved to a profile id. The loading/no-id states below are a defensive
/// fallback, not a path this story's flows are expected to exercise.
class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<String?> profile = ref.watch(learnerProfileProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Profile')),
      body: SafeArea(
        child: profile.maybeWhen(
          data: (String? id) => id == null
              ? const Center(child: CircularProgressIndicator())
              : _ProfileContent(profileId: id),
          orElse: () => const Center(child: CircularProgressIndicator()),
        ),
      ),
    );
  }
}

class _ProfileContent extends StatelessWidget {
  const _ProfileContent({required this.profileId});

  final String profileId;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(AppSpacing.space24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Center(
            child: QrImageView(
              data: profileId,
              size: 200,
              // Required so a real device/screen reader announces this as
              // "profile QR code" rather than silently skipping an image
              // with no label (Accessibility Floor: screen-reader
              // semantics on every meaningful element).
              semanticsLabel: 'QR code encoding your profile UUID',
            ),
          ),
          const SizedBox(height: AppSpacing.space24),
          Text(
            'Your profile ID',
            style: Theme.of(context).textTheme.titleMedium,
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: AppSpacing.space8),
          SelectableText(
            profileId,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                  fontFeatures: const <FontFeature>[FontFeature.tabularFigures()],
                ),
          ),
          const SizedBox(height: AppSpacing.space16),
          SizedBox(
            width: double.infinity,
            child: OutlinedButton.icon(
              onPressed: () => _copyToClipboard(context),
              icon: const Icon(Icons.copy),
              label: const Text('Copy UUID'),
            ),
          ),
          const SizedBox(height: AppSpacing.space24),
          Text(
            kSaveProgressMicrocopy,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyMedium,
          ),
        ],
      ),
    );
  }

  Future<void> _copyToClipboard(BuildContext context) async {
    await Clipboard.setData(ClipboardData(text: profileId));
    if (!context.mounted) {
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Copied to clipboard')),
    );
  }
}
