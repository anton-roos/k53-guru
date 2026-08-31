namespace K53Guru.Application.Features.Attempts.DTOs;

/// <summary>
/// The immediate, per-question feedback returned by CheckAnswerCommand (Story 3.6) -
/// Practice-mode-only. Deliberately reveals the correct option and explanation right away
/// (Practice mode's whole point); never persists a CodeResult/SectionResult - that stays
/// SubmitAttemptCommand's job alone.
/// </summary>
[Description("Check Answer Result")]
public class CheckAnswerResultDto
{
    [Description("Is Correct")] public bool IsCorrect { get; set; }

    [Description("Correct Attempt Answer Option Id")] public int CorrectAttemptAnswerOptionId { get; set; }

    [Description("Explanation")] public string? Explanation { get; set; }
}
