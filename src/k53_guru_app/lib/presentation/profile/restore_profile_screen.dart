import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../../theme/app_spacing.dart';
import '../onboarding/learner_profile_provider.dart';
import 'profile_restore_validator.dart';

/// Shown as a clear, non-technical message per the spec's edge-case matrix
/// ("Invalid... UUID" row) whenever either entry method's input fails the
/// UUID v4 format check.
const String kInvalidProfileIdMessage =
    "That doesn't look like a valid code. Please check and try again.";

/// Offers two entry methods for restoring a learner's profile on a fresh
/// install -- a live QR-scan camera preview (`mobile_scanner`) and a manual
/// text-entry field -- both funnelling through the same
/// [ProfileRestoreValidator], so their behaviour (including the error case)
/// is identical regardless of which one was used.
///
/// [scannerController] is exposed as an injectable constructor parameter
/// purely for testability: production code always uses the default
/// (`autoStart: true`, so the camera starts scanning immediately). Widget
/// tests inject a controller built with `autoStart: false` instead, so the
/// camera preview never calls into `mobile_scanner`'s platform channel at
/// all -- there is no real camera/plugin backend in the widget-test
/// environment, and this story's shared validate-and-persist logic (not the
/// camera widget) is what carries the QR-scan path's test coverage; see
/// `test/presentation/profile_restore_validator_test.dart`.
class RestoreProfileScreen extends ConsumerStatefulWidget {
  const RestoreProfileScreen({super.key, MobileScannerController? scannerController})
      : _injectedScannerController = scannerController;

  final MobileScannerController? _injectedScannerController;

  @override
  ConsumerState<RestoreProfileScreen> createState() =>
      _RestoreProfileScreenState();
}

class _RestoreProfileScreenState extends ConsumerState<RestoreProfileScreen> {
  late final MobileScannerController _scannerController =
      widget._injectedScannerController ?? MobileScannerController();
  final TextEditingController _manualInputController = TextEditingController();
  final ProfileRestoreValidator _validator = const ProfileRestoreValidator();

  bool _isProcessing = false;
  String? _errorMessage;

  @override
  void dispose() {
    _scannerController.dispose();
    _manualInputController.dispose();
    super.dispose();
  }

  /// The single entry point both the QR-scan and manual-paste paths call --
  /// guarantees identical behaviour regardless of which one supplied
  /// [rawInput].
  Future<void> _attemptRestore(String rawInput) async {
    if (_isProcessing) {
      // A QR code stays in the camera's frame for many detection callbacks
      // in a row; without this guard each one would race to validate and
      // persist concurrently.
      return;
    }

    setState(() {
      _isProcessing = true;
      _errorMessage = null;
    });

    final ProfileRestoreResult result = await _validator.validateAndPersist(
      rawInput,
      ref.read(learnerProfileProvider.notifier),
    );

    if (!mounted) {
      return;
    }

    if (result == ProfileRestoreResult.restored) {
      // The root router in `main.dart` watches `learnerProfileProvider` and
      // has already swapped its `home` to `AppShell` by this point (the
      // validator just updated that provider's state synchronously above);
      // popping this pushed route reveals it.
      Navigator.of(context).pop();
      return;
    }

    setState(() {
      _isProcessing = false;
      _errorMessage = kInvalidProfileIdMessage;
    });
  }

  void _onBarcodeDetected(BarcodeCapture capture) {
    final String? rawValue = capture.barcodes.isEmpty
        ? null
        : capture.barcodes.first.rawValue;
    if (rawValue == null || rawValue.isEmpty) {
      return;
    }
    unawaited(_attemptRestore(rawValue));
  }

  void _onManualSubmit() {
    unawaited(_attemptRestore(_manualInputController.text));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Restore profile')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.space24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              Text(
                'Scan your QR code',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: AppSpacing.space12),
              ClipRRect(
                borderRadius: BorderRadius.circular(AppSpacing.space12),
                child: SizedBox(
                  height: 260,
                  child: MobileScanner(
                    controller: _scannerController,
                    onDetect: _onBarcodeDetected,
                  ),
                ),
              ),
              const SizedBox(height: AppSpacing.space32),
              Row(
                children: <Widget>[
                  const Expanded(child: Divider()),
                  Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.space12,
                    ),
                    child: Text(
                      'or',
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ),
                  const Expanded(child: Divider()),
                ],
              ),
              const SizedBox(height: AppSpacing.space32),
              Text(
                'Paste your profile UUID',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: AppSpacing.space12),
              TextField(
                controller: _manualInputController,
                enabled: !_isProcessing,
                autocorrect: false,
                textInputAction: TextInputAction.done,
                onSubmitted: (_) => _onManualSubmit(),
                decoration: const InputDecoration(
                  hintText: 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx',
                  border: OutlineInputBorder(),
                ),
              ),
              if (_errorMessage != null) ...<Widget>[
                const SizedBox(height: AppSpacing.space12),
                Text(
                  _errorMessage!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ],
              const SizedBox(height: AppSpacing.space16),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: _isProcessing ? null : _onManualSubmit,
                  child: _isProcessing
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Restore'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
