// Proves the full DI chain end-to-end:
// SittingsListScreen -> availableSittingsProvider -> sittingsRepositoryProvider
// -> k53ApiClientProvider -> K53ApiClient -> http.
//
// Unlike widget_test.dart (which overrides availableSittingsProvider
// directly, bypassing every layer beneath it), this test overrides only
// k53ApiClientProvider with a K53ApiClient backed by a mocked http.Client,
// and leaves availableSittingsProvider/sittingsRepositoryProvider as their
// real implementations -- so a bug that breaks the wiring between any of
// these layers (e.g. a provider constructing its own client instead of
// reading k53ApiClientProvider) would fail this test even though it passes
// every existing test.

import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:k53_guru_app/data/api/api_config.dart';
import 'package:k53_guru_app/data/api/k53_api_client.dart';
import 'package:k53_guru_app/data/repository/providers.dart';
import 'package:k53_guru_app/presentation/sittings/sittings_list_screen.dart';

void main() {
  testWidgets(
      'SittingsListScreen renders data fetched through the real '
      'availableSittingsProvider -> sittingsRepositoryProvider -> '
      'k53ApiClientProvider chain', (WidgetTester tester) async {
    final http.Client mock = MockClient((http.Request request) async {
      expect(request.method, 'GET');
      expect(request.url.toString(), '${ApiConfig.baseUrl}/sittings');
      return http.Response(
        jsonEncode(<Map<String, dynamic>>[
          <String, dynamic>{
            'id': 1,
            'codes': 'Code1, Code2',
            'name': 'Wired Combo Sitting',
          },
        ]),
        200,
        headers: <String, String>{'content-type': 'application/json'},
      );
    });

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          k53ApiClientProvider.overrideWithValue(
            K53ApiClient(httpClient: mock),
          ),
        ],
        child: const MaterialApp(home: SittingsListScreen()),
      ),
    );

    // First frame: the future hasn't resolved yet.
    expect(find.text('Available Sittings'), findsOneWidget);

    await tester.pumpAndSettle();

    // Data fetched through the full, non-overridden repository/provider
    // chain actually reaches the screen.
    expect(find.text('Wired Combo Sitting'), findsOneWidget);
    expect(find.text('Code 1 + Code 2'), findsOneWidget);
  });
}
