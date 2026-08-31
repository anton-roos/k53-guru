using K53Guru.Domain.Common.Entities;
using K53Guru.Domain.Enums;

namespace K53Guru.Domain.Entities;

/// <summary>
/// A single section's question count and pass mark within a <see cref="TestConfig"/>.
/// Reuses <see cref="Question"/>'s existing plain <see cref="SectionType"/> enum (not
/// <see cref="TestSectionScope"/>) since a rule always names exactly one section.
/// Seeded values are provisional placeholders - see the seeding comments in
/// ApplicationDbContextInitializer.SeedTestConfigsAsync, traced to test-structure.md and
/// deferred-work.md (spec-3-2-configure-test-parameters.md entry).
/// </summary>
public class SectionRule : BaseAuditableEntity
{
    public int TestConfigId { get; set; }

    public SectionType Section { get; set; }

    /// <summary>
    /// Number of questions drawn for this section on a sitting.
    /// PROVISIONAL PLACEHOLDER: test-structure.md documents an official *range* per section
    /// (Rules/Signs 28-30, VehicleControls 8-12), not a fixed number. This seeds the upper
    /// end of each range as a single representative value, per explicit human direction -
    /// no range modeling. Confirm against a live DLTC/CLLT terminal before relying on this.
    /// </summary>
    public int QuestionCount { get; set; }

    /// <summary>
    /// Minimum correct answers required to pass this section.
    /// PROVISIONAL PLACEHOLDER: paired with the QuestionCount placeholder above per
    /// test-structure.md (Rules: 22 of 30, Signs: 23 of 30, VehicleControls: 10 of 12).
    /// </summary>
    public int PassMark { get; set; }
}
