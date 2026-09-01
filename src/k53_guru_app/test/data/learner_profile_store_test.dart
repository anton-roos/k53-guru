// Verifies `LearnerProfileStore`'s read/write round-trip and the spec's
// edge case: "Local storage read fails: `SharedPreferences` throws or
// returns corrupt data -> Treated as 'no profile id' (first-run path),
// never crashes."
//
// Story 4.5 adds the same coverage for `readLicenceCode`/`writeLicenceCode`,
// including the spec's edge-case matrix row "Local storage read fails for
// code -> treated as 'no code chosen', same graceful-degradation pattern
// as the profile id above."

import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/data/local/learner_profile_store.dart';
import 'package:k53_guru_app/domain/licence_code.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const LearnerProfileStore store = LearnerProfileStore();

  test('readProfileId returns null on a fresh install (nothing persisted)',
      () async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    expect(await store.readProfileId(), isNull);
  });

  test('writeProfileId then readProfileId round-trips the exact same id',
      () async {
    SharedPreferences.setMockInitialValues(<String, Object>{});
    const String id = '11111111-2222-4333-8444-555555555555';

    await store.writeProfileId(id);

    expect(await store.readProfileId(), id);
  });

  test(
      'readProfileId treats a value of the wrong type ("corrupt data") as '
      'absent, and never throws', () async {
    // A non-String value under the same key simulates local storage
    // holding corrupt/incompatible data.
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_profile_id': 12345,
    });

    expect(await store.readProfileId(), isNull);
  });

  test('readProfileId treats an empty string as absent', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_profile_id': '',
    });

    expect(await store.readProfileId(), isNull);
  });

  test(
      'readProfileId treats a non-empty, non-UUID string ("corrupt data" '
      'from a partial disk write) as absent, and never throws', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_profile_id': 'abc123',
    });

    expect(await store.readProfileId(), isNull);
  });

  test('readLicenceCode returns null on a fresh install (nothing persisted)',
      () async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    expect(await store.readLicenceCode(), isNull);
  });

  test(
      'writeLicenceCode then readLicenceCode round-trips the exact same '
      'code, for every LicenceCode value', () async {
    for (final LicenceCode code in LicenceCode.values) {
      SharedPreferences.setMockInitialValues(<String, Object>{});

      await store.writeLicenceCode(code);

      expect(await store.readLicenceCode(), code);
    }
  });

  test(
      'writeLicenceCode replaces a previously-persisted code (the '
      '"Change code" flow)', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{});
    await store.writeLicenceCode(LicenceCode.code1);
    expect(await store.readLicenceCode(), LicenceCode.code1);

    await store.writeLicenceCode(LicenceCode.code3);

    expect(await store.readLicenceCode(), LicenceCode.code3);
  });

  test(
      'readLicenceCode treats a value of the wrong type ("corrupt data") '
      'as absent, and never throws', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_licence_code': 12345,
    });

    expect(await store.readLicenceCode(), isNull);
  });

  test('readLicenceCode treats an empty string as absent', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_licence_code': '',
    });

    expect(await store.readLicenceCode(), isNull);
  });

  test(
      'readLicenceCode treats a non-empty, unrecognised string ("corrupt '
      'data" from a partial disk write) as absent, and never throws',
      () async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'learner_licence_code': 'NotARealCode',
    });

    expect(await store.readLicenceCode(), isNull);
  });
}
