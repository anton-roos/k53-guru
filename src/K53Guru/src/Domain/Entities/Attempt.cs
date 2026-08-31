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
    /// The licence code(s) this attempt is sitting, copied from Test.Codes at start time. Either a
    /// single code (Code1/Code2/Code3) or a valid combination (Code1|Code2, Code1|Code3) - see
    /// StartAttemptCommand for the full allowlist (Story 3.4).
    /// </summary>
    public LicenceCode Code { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Set by SubmitAttemptCommand (Story 3.5) once the attempt has been graded; null while
    /// in-progress. A second submit against an already-submitted Attempt (non-null here) is
    /// rejected outright, never re-graded.
    /// </summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>
    /// Which learner experience this attempt is sitting under (Story 3.6), set once at
    /// StartAttemptCommand time and never changed thereafter. Practice mode permits
    /// CheckAnswerCommand's immediate correctness/explanation + retry and never enforces a time
    /// limit; Test mode withholds correctness/explanations until submission and enforces
    /// SubmitAttemptCommand's server-side time-limit check.
    /// </summary>
    public AttemptMode Mode { get; set; }

    /// <summary>
    /// Client-supplied submission timestamp (Story 3.6), stored for DIAGNOSTICS ONLY - the
    /// server's own late-submission check in SubmitAttemptCommand always uses server
    /// DateTime.UtcNow, never this value. Optional; null if the client didn't supply one.
    /// </summary>
    public DateTime? ClientSubmittedAt { get; set; }

    public List<AttemptQuestion> AttemptQuestions { get; set; } = new();
}
