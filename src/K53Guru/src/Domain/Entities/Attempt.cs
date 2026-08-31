using K53Guru.Domain.Common.Entities;
using K53Guru.Domain.Enums;

namespace K53Guru.Domain.Entities;

/// <summary>
/// A single learner's sitting of a <see cref="Test"/>, composed and frozen at start time
/// (Story 3.3). Owns an immutable snapshot of the questions it was composed from
/// (<see cref="AttemptQuestions"/>) - later edits to the source <see cref="Entities.Question"/>s
/// never mutate an in-progress or resumed attempt.
/// </summary>
public class Attempt : BaseAuditableEntity
{
    public Guid LearnerProfileId { get; set; }

    /// <summary>
    /// The source Test this attempt was composed from. Traceability only - the attempt's actual
    /// content lives entirely in the owned <see cref="AttemptQuestions"/> snapshot below, never
    /// re-read live from this Test.
    /// </summary>
    public int TestId { get; set; }

    /// <summary>
    /// The single licence code this attempt is sitting, copied from Test.Codes at start time.
    /// Always exactly one bit (Code1/Code2/Code3) - a combination sitting is Story 3.4's concern.
    /// </summary>
    public LicenceCode Code { get; set; }

    public DateTime StartedAt { get; set; }

    public List<AttemptQuestion> AttemptQuestions { get; set; } = new();
}
