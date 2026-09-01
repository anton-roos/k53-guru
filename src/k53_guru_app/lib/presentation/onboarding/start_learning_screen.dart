import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../theme/app_spacing.dart';
import '../profile/restore_profile_screen.dart';
import 'learner_profile_provider.dart';

/// The first-run gate (EXPERIENCE.md: "one `Start learning` CTA; nothing
/// else to decide" -- Story 4.4 adds a second, clearly-less-prominent path
/// below it: `Restore profile`, for a learner who already has a profile id
/// from a previous install/device). A single centered primary button using
/// the theme's `display`/`button-primary` styles; tapping it generates a
/// new UUID v4, persists it via `learner_profile_store.dart`, and updates
/// [learnerProfileProvider]'s state -- the root router in `main.dart`
/// watches that same provider and swaps this screen out for `AppShell`
/// automatically once it resolves to a non-null id.
///
/// No account, password, form field, or any other input is ever collected
/// by the primary path -- the button press is the entire interaction. The
/// secondary `Restore profile` action opens `RestoreProfileScreen`, which
/// funnels both its QR-scan and manual-paste entry methods through the same
/// shared validate-and-persist logic (`profile_restore_validator.dart`)
/// that this screen's own generation path also ultimately updates.
class StartLearningScreen extends ConsumerStatefulWidget {
  const StartLearningScreen({super.key});

  @override
  ConsumerState<StartLearningScreen> createState() =>
      _StartLearningScreenState();
}

class _StartLearningScreenState extends ConsumerState<StartLearningScreen> {
  // Guards against rapid double-tap: true from the moment a generation is
  // kicked off until it resolves. Deliberately a local widget flag rather
  // than routed through `learnerProfileProvider`'s own AsyncValue -- this
  // screen is only ever shown while that provider is AsyncData(null), and
  // flipping its *shared* state to loading mid-tap would also be observed
  // by the root router in `main.dart` (which treats "loading" as its
  // `orElse` fallback), swapping this screen out for the launching spinner
  // on every single tap, not just a double-tap.
  bool _isGenerating = false;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      // Accessibility floor (Story 4.7): this screen used to lay its Column
      // out directly inside a fixed-height `Center`/`Padding`/`SafeArea`
      // with no way to grow -- a genuine `RenderFlex` overflow at large
      // dynamic-type scales on a short/narrow viewport (the same bug class
      // `LicenceCodeSelectionScreen` already fixes). Same `LayoutBuilder` +
      // `SingleChildScrollView` + `ConstrainedBox` with `minHeight` pattern:
      // preserves this screen's vertically-centered look whenever the
      // content actually fits (the common case, unaffected by this change),
      // while letting it scroll instead of clipping/overflowing once it
      // doesn't.
      body: SafeArea(
        child: LayoutBuilder(
          builder: (BuildContext context, BoxConstraints constraints) {
            final double minContentHeight = constraints.maxHeight -
                (AppSpacing.space24 * 2);
            return SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.space24),
              child: ConstrainedBox(
                constraints: BoxConstraints(
                  minHeight: minContentHeight > 0 ? minContentHeight : 0,
                ),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      'K53 Guru',
                      style: Theme.of(context).textTheme.displayLarge,
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: AppSpacing.space32),
                    SizedBox(
                      width: double.infinity,
                      child: ElevatedButton(
                        onPressed: _isGenerating ? null : _startLearning,
                        child: const Text('Start learning'),
                      ),
                    ),
                    const SizedBox(height: AppSpacing.space16),
                    TextButton(
                      onPressed: _isGenerating
                          ? null
                          : () => _openRestoreProfile(context),
                      child: const Text('Restore profile'),
                    ),
                  ],
                ),
              ),
            );
          },
        ),
      ),
    );
  }

  void _startLearning() {
    setState(() => _isGenerating = true);
    unawaited(
      ref.read(learnerProfileProvider.notifier).generateAndPersistProfileId(),
    );
  }

  void _openRestoreProfile(BuildContext context) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => const RestoreProfileScreen(),
      ),
    );
  }
}
