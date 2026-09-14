using K53Guru.Domain.Common.Entities;

namespace K53Guru.Domain.Entities;

/// <summary>
/// An owned, immutable COPY of a source <see cref="AnswerOption"/> at snapshot time. Shuffled at
/// attempt-start time (StartAttemptCommand) so the admin-authored order (which tends to place the
/// correct option first) isn't visible to the learner; <see cref="Order"/> reflects that
/// per-attempt shuffle, not the source AnswerOption.Order.
/// </summary>
public class AttemptAnswerOption : BaseAuditableEntity
{
    public int AttemptQuestionId { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Retained for grading (Story 3.5) - never serialized into AttemptAnswerOptionDto.
    /// </summary>
    public bool IsCorrect { get; set; }

    /// <summary>
    /// Position within the owning question's answer options for this attempt, after the
    /// per-attempt shuffle applied at start time - frozen thereafter (a resume must show the same
    /// order every time, per GetAttemptQuery's contract).
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// The learner's recorded choice, set by SubmitAttemptCommand (Story 3.5) - defaults false
    /// until submission. Never exposed back out through AttemptDto, same as IsCorrect.
    /// </summary>
    public bool IsSelected { get; set; }
}
