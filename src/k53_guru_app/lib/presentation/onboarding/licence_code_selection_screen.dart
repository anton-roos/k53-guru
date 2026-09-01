import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/licence_code.dart';
import '../../theme/app_colors_extension.dart';
import '../../theme/app_spacing.dart';
import 'licence_code_provider.dart';

/// The mandatory first-run step that follows profile-id establishment
/// (Story 4.3/4.4), whether the id was freshly generated or restored: pick
/// exactly one of Code 1/2/3. Per EXPERIENCE.md's "Empty / first-run"
/// pattern -- "nothing else to decide" -- there is no separate "confirm"
/// step: tapping an option IS the confirmation. It persists the choice via
/// [licenceCodeProvider] and the root router in `main.dart` (which watches
/// that same provider) swaps this screen out for `AppShell` automatically.
///
/// Also reused by the Profile tab's `Change code` flow
/// (`profile_screen.dart`): after the learner confirms `Recalibrate` or
/// `Start fresh`, this same screen is *pushed* on top of `AppShell` to pick
/// the new code. [_select] tells the two call sites apart by whether this
/// route can be popped: the first-run case is the router's own `home`
/// (nothing to pop -- the router swaps it out on its own once the provider
/// resolves), while the change-code case was reached via `Navigator.push`
/// and must pop itself to reveal Profile again once the new code is
/// persisted.
class LicenceCodeSelectionScreen extends ConsumerStatefulWidget {
  const LicenceCodeSelectionScreen({super.key});

  @override
  ConsumerState<LicenceCodeSelectionScreen> createState() =>
      _LicenceCodeSelectionScreenState();
}

class _LicenceCodeSelectionScreenState
    extends ConsumerState<LicenceCodeSelectionScreen> {
  // Guards against a rapid double-tap across two different option cards
  // firing two overlapping selections -- same rationale/shape as
  // `StartLearningScreen._isGenerating`: a local widget flag, not routed
  // through the shared provider's own AsyncValue, so a mid-tap state
  // change here doesn't also flicker whatever screen is watching
  // `licenceCodeProvider` elsewhere.
  bool _isSelecting = false;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      // Accessibility floor (Story 4.7): this screen's headline/subtitle
      // grow considerably at large dynamic-type scales (e.g. a 200% OS text
      // setting), and this `Column` used to be laid out directly inside a
      // fixed-height `Padding`/`SafeArea` with no way to grow -- a genuine
      // `RenderFlex` overflow at 200% scale. `LayoutBuilder` +
      // `SingleChildScrollView` + a `ConstrainedBox` with `minHeight`
      // preserves the original vertically-centered look whenever the
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
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    Text(
                      'Which licence are you studying for?',
                      style: Theme.of(context).textTheme.displayLarge,
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: AppSpacing.space8),
                    Text(
                      "Pick one -- we'll show you exactly what you need.",
                      style: Theme.of(context).textTheme.bodyLarge,
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: AppSpacing.space32),
                    _OptionCard(
                      label: 'Code 1',
                      enabled: !_isSelecting,
                      onTap: () => _select(LicenceCode.code1),
                    ),
                    const SizedBox(height: AppSpacing.space16),
                    _OptionCard(
                      label: 'Code 2',
                      enabled: !_isSelecting,
                      onTap: () => _select(LicenceCode.code2),
                    ),
                    const SizedBox(height: AppSpacing.space16),
                    _OptionCard(
                      label: 'Code 3',
                      enabled: !_isSelecting,
                      onTap: () => _select(LicenceCode.code3),
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

  Future<void> _select(LicenceCode code) async {
    if (_isSelecting) {
      return;
    }
    setState(() => _isSelecting = true);
    await ref.read(licenceCodeProvider.notifier).selectLicenceCode(code);

    if (!mounted) {
      return;
    }
    // Change-code flow: this screen was pushed on top of `AppShell`/Profile
    // and must pop itself now that the new code is persisted. First-run
    // flow: this screen IS the router's `home` in `main.dart`, which has
    // nothing to pop -- it swaps itself out for `AppShell` on its own once
    // `licenceCodeProvider` resolves to the new non-null code.
    if (Navigator.of(context).canPop()) {
      Navigator.of(context).pop();
    }
  }
}

/// One tappable option card, styled from DESIGN.md's `option-card` token
/// (`bg: card`, `border: line`, `radius: md`, `pad: 16px`,
/// `min-height: 56px`) -- the three cards are large and equally-weighted,
/// with no visual hierarchy between them, since picking any one of them is
/// an equally valid, equally final choice.
class _OptionCard extends StatelessWidget {
  const _OptionCard({
    required this.label,
    required this.enabled,
    required this.onTap,
  });

  final String label;
  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: context.appColors.card,
      borderRadius: BorderRadius.circular(AppRadius.md),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: enabled ? onTap : null,
        child: Container(
          constraints: const BoxConstraints(
            minHeight: AppSpacing.minTapTarget,
          ),
          padding: const EdgeInsets.all(AppSpacing.space16),
          decoration: BoxDecoration(
            border: Border.all(color: context.appColors.line),
            borderRadius: BorderRadius.circular(AppRadius.md),
          ),
          alignment: Alignment.center,
          child: Text(
            label,
            // DESIGN.md's `typography.scale.option` (17px/600) -- mapped to
            // `bodyMedium` by `AppTypography.textTheme`.
            style: Theme.of(context).textTheme.bodyMedium,
            textAlign: TextAlign.center,
          ),
        ),
      ),
    );
  }
}
