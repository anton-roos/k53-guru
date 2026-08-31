using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Attempts.DTOs;

/// <summary>
/// The final, versioned grading result returned by SubmitAttemptCommand (Story 3.5). Aggregate,
/// per-code, and per-section pass/fail only - deliberately carries no per-question
/// correctness/explanation, matching the same "no IsCorrect out the wire" default AttemptDto
/// already established. This shape is mode-agnostic; Story 3.6's Practice/Test confidentiality
/// split is a separate, later concern layered on top of it.
/// </summary>
[Description("Graded Attempt Result")]
public class GradedAttemptResultDto
{
    [Description("Attempt Id")] public int AttemptId { get; set; }

    /// <summary>
    /// Overall pass/fail across the whole sitting - true only if every <see cref="CodeResultDto.Passed"/>
    /// is true (a single-code sitting has exactly one CodeResult; a combination has one per
    /// constituent code, per Story 3.4).
    /// </summary>
    [Description("Passed")] public bool Passed { get; set; }

    [Description("Code Results")] public List<CodeResultDto> CodeResults { get; set; } = new();

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<CodeResult, CodeResultDto>(MemberList.None);
            CreateMap<SectionResult, SectionResultDto>(MemberList.None);
        }
    }
}

/// <summary>
/// One constituent code's graded outcome - failing any one of its SectionResults fails the whole
/// code, independently of any other code in a combination sitting (a partial pass is possible).
/// </summary>
public class CodeResultDto
{
    [Description("Code")] public LicenceCode Code { get; set; }

    [Description("Passed")] public bool Passed { get; set; }

    [Description("Section Results")] public List<SectionResultDto> SectionResults { get; set; } = new();
}

/// <summary>
/// One section's score within a CodeResult, graded against the CURRENT TestConfig/SectionRule.PassMark
/// at submission time - not a value snapshotted from Attempt start.
/// </summary>
public class SectionResultDto
{
    [Description("Section")] public SectionType Section { get; set; }

    [Description("Correct Count")] public int CorrectCount { get; set; }

    [Description("Pass Mark")] public int PassMark { get; set; }

    [Description("Passed")] public bool Passed { get; set; }
}
