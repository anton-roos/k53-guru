using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Attempts.DTOs;

/// <summary>
/// The shared response shape for both StartAttemptCommand and GetAttemptQuery (Story 3.3) - a
/// resume must return the identical shape/content as the original start response. Deliberately
/// carries no IsCorrect anywhere: the server retains it for future grading (Story 3.5) but nothing
/// in this story's scope requires or should expose it - a safe default ahead of Story 3.6's
/// Practice/Test confidentiality split.
/// </summary>
[Description("Attempts")]
public class AttemptDto
{
    [Description("Id")] public int Id { get; set; }

    [Description("Code")] public LicenceCode Code { get; set; }

    [Description("Started At")] public DateTime StartedAt { get; set; }

    [Description("Questions")] public List<AttemptQuestionDto> AttemptQuestions { get; set; } = new();

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Attempt, AttemptDto>(MemberList.None);
            CreateMap<AttemptQuestion, AttemptQuestionDto>(MemberList.None);
            CreateMap<AttemptAnswerOption, AttemptAnswerOptionDto>(MemberList.None);
        }
    }
}

/// <summary>
/// One snapshotted question within an Attempt, in <see cref="DisplayOrder"/> order. Carries
/// <see cref="Section"/>/<see cref="DisplayOrder"/> so the client can render "Section N of M"
/// progress without a flat question counter.
/// </summary>
public class AttemptQuestionDto
{
    /// <summary>
    /// Exposed (Story 3.5) so the client can reference which question it's answering when
    /// submitting - see SubmitAttemptCommand.
    /// </summary>
    [Description("Id")] public int Id { get; set; }

    [Description("Section")] public SectionType Section { get; set; }

    /// <summary>
    /// The licence code this question counts toward - the whole combination for shared
    /// Rules/Signs questions, or the one constituent code for a VehicleControls module
    /// question. See <see cref="Domain.Entities.AttemptQuestion.Code"/>.
    /// </summary>
    [Description("Code")] public LicenceCode Code { get; set; }

    [Description("Display Order")] public int DisplayOrder { get; set; }

    [Description("Stem")] public string? Stem { get; set; }

    [Description("Sign Ref")] public string? SignRef { get; set; }

    [Description("Answer Options")] public List<AttemptAnswerOptionDto> AttemptAnswerOptions { get; set; } = new();
}

/// <summary>
/// One snapshotted answer option, in its original (admin-authored) order. Intentionally has no
/// IsCorrect property - see AttemptDto's remarks.
/// </summary>
public class AttemptAnswerOptionDto
{
    /// <summary>
    /// Exposed (Story 3.5) so the client can reference which option it's selecting when
    /// submitting - see SubmitAttemptCommand.
    /// </summary>
    [Description("Id")] public int Id { get; set; }

    [Description("Text")] public string? Text { get; set; }

    [Description("Order")] public int Order { get; set; }
}
