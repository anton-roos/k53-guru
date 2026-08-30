using K53Guru.Domain.Common.Entities;

namespace K53Guru.Domain.Entities;

public class AnswerOption : BaseAuditableEntity
{
    public int QuestionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }

    /// <summary>
    /// Position within the question's ordered set of answer options.
    /// </summary>
    public int Order { get; set; }
}
