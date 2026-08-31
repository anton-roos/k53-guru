import 'package:flutter/material.dart';

/// Placeholder content for the Test tab. Real Test-mode UI is Epic 6's job
/// -- this story only builds the navigation shell around it.
class TestModePlaceholderScreen extends StatelessWidget {
  const TestModePlaceholderScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Text('Test', style: Theme.of(context).textTheme.displayLarge),
      ),
    );
  }
}
