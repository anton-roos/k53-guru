using K53Guru.Domain.Common.Entities;

namespace K53Guru.Domain.Entities;

public class TestQuestion : BaseAuditableEntity
{
    public int TestId { get; set; }
    public int QuestionId { get; set; }

    /// <summary>
    /// Navigation to the referenced Question - required so the grouped-view projection
    /// (TestByIdSpecification) can surface Stem/Codes/Section per associated question.
    /// </summary>
    public Question Question { get; set; } = null!;
}
