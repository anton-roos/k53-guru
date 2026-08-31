using K53Guru.Domain.Common.Entities;
using K53Guru.Domain.Enums;

namespace K53Guru.Domain.Entities;

/// <summary>
/// Per-<see cref="LicenceCode"/> test fidelity parameters (time limit + section rules),
/// read as data by attempt composition (Story 3.3) and grading (Story 3.5) rather than
/// hardcoded. Seeded values are provisional placeholders - see <see cref="SectionRule"/>
/// and the seeding comments in ApplicationDbContextInitializer.SeedTestConfigsAsync,
/// traced to test-structure.md and deferred-work.md.
/// </summary>
public class TestConfig : BaseAuditableEntity
{
    /// <summary>
    /// Single licence code this config applies to (Code1, Code2, or Code3). Reuses the
    /// existing <see cref="LicenceCode"/> flags enum - as with <see cref="Test.Codes"/> -
    /// but a TestConfig row always names exactly one code, never a combination.
    /// </summary>
    public LicenceCode Code { get; set; }

    /// <summary>
    /// Time limit for this code's sitting, in minutes.
    /// PROVISIONAL PLACEHOLDER (60 minutes): test-structure.md documents no time limit at
    /// all ("Not specified in the CLLT description provided; unconfirmed... Confirm before
    /// enforcing."). See deferred-work.md (spec-3-2-configure-test-parameters.md entry).
    /// </summary>
    public int TimeLimitMinutes { get; set; }

    public List<SectionRule> SectionRules { get; set; } = new();
}
