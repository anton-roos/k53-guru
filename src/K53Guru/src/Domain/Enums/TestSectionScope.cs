namespace K53Guru.Domain.Enums;

/// <summary>
/// The K53 test section(s) a <see cref="Entities.Test"/> covers. A test may span one or more
/// sections, so this is a bit-flags enum stored as its combined value - distinct from
/// <see cref="SectionType"/>, which is a plain (non-flags) per-question section and must not be
/// repurposed for this.
/// </summary>
[Flags]
public enum TestSectionScope
{
    None = 0,
    Rules = 1,
    Signs = 2,
    VehicleControls = 4
}
