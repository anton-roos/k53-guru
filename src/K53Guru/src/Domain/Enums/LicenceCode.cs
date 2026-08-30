namespace K53Guru.Domain.Enums;

/// <summary>
/// The K53 learner/driving licence code(s) a <see cref="Entities.Question"/> applies to.
/// A question may apply to one or more codes (e.g. a Rules-section question shared across
/// Code1 and Code2), so this is a bit-flags enum stored as its combined value.
/// </summary>
[Flags]
public enum LicenceCode
{
    None = 0,
    Code1 = 1,
    Code2 = 2,
    Code3 = 4
}
