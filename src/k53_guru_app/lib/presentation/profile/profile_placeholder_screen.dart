import 'package:flutter/material.dart';

/// Placeholder content for the Profile tab. Real Profile UI (dark-mode
/// toggle, progress, etc.) is a later Epic 4 story's job -- this story only
/// builds the navigation shell around it.
class ProfilePlaceholderScreen extends StatelessWidget {
  const ProfilePlaceholderScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Text('Profile', style: Theme.of(context).textTheme.displayLarge),
      ),
    );
  }
}
