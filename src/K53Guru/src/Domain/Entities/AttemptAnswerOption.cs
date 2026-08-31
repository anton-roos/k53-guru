using K53Guru.Domain.Common.Entities;

namespace K53Guru.Domain.Entities;

/// <summary>
/// An owned, immutable COPY of a source <see cref="AnswerOption"/> at snapshot time, in its
/// original (admin-authored) order. Never shuffled - only question selection/order within a
/// section is randomised, not answer-option order within a question.
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
    /// Position within the owning question's ordered set of answer options - copied verbatim from
    /// the source AnswerOption.Order.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// The learner's recorded choice, set by SubmitAttemptCommand (Story 3.5) - defaults false
    /// until submission. Never exposed back out through AttemptDto, same as IsCorrect.
    /// </summary>
    public bool IsSelected { get; set; }
}
