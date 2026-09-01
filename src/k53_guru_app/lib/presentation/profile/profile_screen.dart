import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../../domain/licence_code.dart';
import '../../theme/app_spacing.dart';
import '../onboarding/learner_profile_provider.dart';
import '../onboarding/licence_code_provider.dart';
import '../onboarding/licence_code_selection_screen.dart';
import '../settings/theme_mode_provider.dart';
import '../settings/tts_settings_provider.dart';

/// The exact save-your-progress microcopy from EXPERIENCE.md's "Identity &
/// Profile" section -- reused verbatim rather than re-typed inline so there
/// is exactly one place this string can drift from the spec.
const String kSaveProgressMicrocopy =
    'To save your progress, copy this UUID to import your results in '
    'another app';

/// The two named choices the `Change code` confirmation dialog offers, per
/// the spec's acceptance criteria. Both are currently no-ops beyond
/// persisting the newly-picked code: no progress/mastery data model exists
/// yet (Epic 5/6, not built this session) for either to actually recalibrate
/// or reset. The interaction itself -- the dialog, these two exact choices,
/// re-presenting the code picker -- is built faithfully so a future story
/// only needs to implement the two branches' real data effects.
enum ChangeCodeChoice { recalibrate, startFresh }

/// The exact label shown for each [LicenceCode], reused wherever the
/// learner's current code needs to be displayed in the Profile tab.
String licenceCodeLabel(LicenceCode code) {
  switch (code) {
    case LicenceCode.code1:
      return 'Code 1';
    case LicenceCode.code2:
      return 'Code 2';
    case LicenceCode.code3:
      return 'Code 3';
  }
}

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

class _ProfileContent extends ConsumerStatefulWidget {
  const _ProfileContent({required this.profileId});

  final String profileId;

  @override
  ConsumerState<_ProfileContent> createState() => _ProfileContentState();
}

class _ProfileContentState extends ConsumerState<_ProfileContent> {
  // Guards against rapid double-tap on the `Change code` row -- same
  // rationale/shape as `StartLearningScreen._isGenerating`,
  // `LicenceCodeSelectionScreen._isSelecting`, and
  // `RestoreProfileScreen._isProcessing`: a local widget flag, checked and
  // set before `showDialog` opens, so two quick taps can't stack two
  // `AlertDialog`s on the Navigator. Reset once the flow completes --
  // whether the dialog was dismissed without choosing, or a full
  // recalibrate/reset selection was made.
  bool _isChangingCode = false;

  @override
  Widget build(BuildContext context) {
    final AsyncValue<LicenceCode?> licenceCode = ref.watch(licenceCodeProvider);
    final AsyncValue<ThemeMode> themeMode = ref.watch(themeModeProvider);
    final AsyncValue<bool> ttsEnabled = ref.watch(ttsSettingsProvider);

    // Defaults the visual selection to Light while the provider hasn't
    // resolved yet (or errored), matching SettingsStore's documented
    // default -- never crashes waiting on AsyncLoading/AsyncError.
    final ThemeMode selectedThemeMode = themeMode.maybeWhen(
      data: (ThemeMode mode) => mode,
      orElse: () => ThemeMode.light,
    );
    final bool ttsSwitchValue = ttsEnabled.maybeWhen(
      data: (bool enabled) => enabled,
      orElse: () => false,
    );

    return SingleChildScrollView(
      padding: const EdgeInsets.all(AppSpacing.space24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Center(
            child: QrImageView(
              data: widget.profileId,
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
            widget.profileId,
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
          const SizedBox(height: AppSpacing.space32),
          const Divider(),
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('Change code'),
            subtitle: Text(
              licenceCode.maybeWhen(
                data: (LicenceCode? code) =>
                    code == null ? 'Not set' : licenceCodeLabel(code),
                orElse: () => 'Loading...',
              ),
            ),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => _onChangeCodeTapped(context),
          ),
          const SizedBox(height: AppSpacing.space32),
          const Divider(),
          Text(
            'Settings',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: AppSpacing.space16),
          Semantics(
            label: 'Theme',
            child: SegmentedButton<ThemeMode>(
              key: const Key('themeModeSegmentedButton'),
              segments: const <ButtonSegment<ThemeMode>>[
                ButtonSegment<ThemeMode>(
                  value: ThemeMode.light,
                  label: Text('Light'),
                  icon: Icon(Icons.light_mode),
                ),
                ButtonSegment<ThemeMode>(
                  value: ThemeMode.dark,
                  label: Text('Dark'),
                  icon: Icon(Icons.dark_mode),
                ),
              ],
              selected: <ThemeMode>{selectedThemeMode},
              onSelectionChanged: (Set<ThemeMode> selection) {
                ref
                    .read(themeModeProvider.notifier)
                    .setThemeMode(selection.first);
              },
            ),
          ),
          const SizedBox(height: AppSpacing.space16),
          SwitchListTile(
            key: const Key('ttsEnabledSwitch'),
            contentPadding: EdgeInsets.zero,
            title: const Text('Read questions aloud'),
            subtitle: const Text(
              'Applies once practice and test questions are available',
            ),
            value: ttsSwitchValue,
            onChanged: (bool enabled) {
              ref.read(ttsSettingsProvider.notifier).setTtsEnabled(enabled);
            },
          ),
        ],
      ),
    );
  }

  Future<void> _copyToClipboard(BuildContext context) async {
    await Clipboard.setData(ClipboardData(text: widget.profileId));
    if (!context.mounted) {
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Copied to clipboard')),
    );
  }

  /// Opens the confirmation dialog the AC requires -- exactly two named
  /// choices, `Recalibrate` and `Start fresh` -- then, regardless of which
  /// one the learner picks, re-presents [LicenceCodeSelectionScreen] to
  /// choose the new code. Both choices are currently no-ops beyond that
  /// (see [ChangeCodeChoice]'s doc comment); dismissing the dialog without
  /// choosing either (tap outside, back button) leaves the current code
  /// untouched.
  ///
  /// Guarded by [_isChangingCode] against rapid double-tap: the flag is set
  /// synchronously before `showDialog` is awaited, so a second tap landing
  /// before the first `setState` is even rebuilt still sees it and bails
  /// out, rather than stacking a second `AlertDialog` on the Navigator.
  Future<void> _onChangeCodeTapped(BuildContext context) async {
    if (_isChangingCode) {
      return;
    }
    setState(() => _isChangingCode = true);

    final ChangeCodeChoice? choice = await showDialog<ChangeCodeChoice>(
      context: context,
      builder: (BuildContext dialogContext) => AlertDialog(
        title: const Text('Change your code'),
        content: const Text(
          'Recalibrate maps your existing progress to the new code, or '
          'start fresh to reset it.',
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext)
                .pop(ChangeCodeChoice.recalibrate),
            child: const Text('Recalibrate'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext)
                .pop(ChangeCodeChoice.startFresh),
            child: const Text('Start fresh'),
          ),
        ],
      ),
    );

    if (choice == null) {
      if (mounted) {
        setState(() => _isChangingCode = false);
      }
      return;
    }

    if (!context.mounted) {
      return;
    }

    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => const LicenceCodeSelectionScreen(),
      ),
    );

    if (mounted) {
      setState(() => _isChangingCode = false);
    }
  }
}
