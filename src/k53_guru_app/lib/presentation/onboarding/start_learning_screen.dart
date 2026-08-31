import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../theme/app_spacing.dart';
import 'learner_profile_provider.dart';

/// The first-run gate (EXPERIENCE.md: "one `Start learning` CTA; nothing
/// else to decide"). A single centered button using the theme's
/// `display`/`button-primary` styles; tapping it generates a new UUID v4,
/// persists it via `learner_profile_store.dart`, and updates
/// [learnerProfileProvider]'s state -- the root router in `main.dart`
/// watches that same provider and swaps this screen out for `AppShell`
/// automatically once it resolves to a non-null id.
///
/// No account, password, form field, or any other input is ever
/// collected -- the button press is the entire interaction.
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
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.space24),
            child: Column(
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
              ],
            ),
          ),
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
}
