using K53Guru.Domain.Common.Entities;
using K53Guru.Domain.Enums;

namespace K53Guru.Domain.Entities;

/// <summary>
/// One section's score within a <see cref="CodeResult"/>, produced by SubmitAttemptCommand
/// (Story 3.5) against the CURRENT TestConfig/SectionRule.PassMark at grading time - never
/// snapshotted at Attempt start, per Story 3.2's own "scoring reads config live" AC.
/// </summary>
public class SectionResult : BaseAuditableEntity
{
    public int CodeResultId { get; set; }

    public SectionType Section { get; set; }

    /// <summary>
    /// Number of this section's AttemptQuestions the learner answered correctly.
    /// </summary>
    public int CorrectCount { get; set; }

    /// <summary>
    /// The pass-mark threshold applied at grading time, copied from the live SectionRule.PassMark
    /// for transparency - recorded here even though it is not snapshotted from the config (a later
    /// change to the config's PassMark never rewrites an already-graded SectionResult).
    /// </summary>
    public int PassMark { get; set; }

    public bool Passed { get; set; }
}
