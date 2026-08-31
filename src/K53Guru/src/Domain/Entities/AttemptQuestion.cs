using K53Guru.Domain.Common.Entities;
using K53Guru.Domain.Enums;

namespace K53Guru.Domain.Entities;

/// <summary>
/// An owned, immutable COPY of a source <see cref="Question"/>'s content at the moment its
/// owning <see cref="Attempt"/> was started - never a live FK re-read of Question. Later edits to
/// the source Question never change an already-snapshotted AttemptQuestion.
/// </summary>
public class AttemptQuestion : BaseAuditableEntity
{
    public int AttemptId { get; set; }

    /// <summary>
    /// The source Question this snapshot was copied from. Traceability only.
    /// </summary>
    public int QuestionId { get; set; }

    public SectionType Section { get; set; }

    /// <summary>
    /// The licence code this question counts toward. For Rules/Signs questions (shared across a
    /// combination attempt), this is the full <see cref="Attempt.Code"/> value - the whole
    /// combination, or the single code. For VehicleControls questions, this is the ONE specific
    /// constituent code that question's module belongs to (Story 3.4) - lets a future grader
    /// (Story 3.5) tell which code's VehicleControls module a question belongs to, and apply the
    /// one shared Rules/Signs result to every code in the attempt.
    /// <para>
    /// Because of this dual meaning, code-scoped filtering or grouping over a combination
    /// attempt's questions MUST use <c>Code.HasFlag(code)</c> - never <c>Code == code</c>, and
    /// never a plain <c>GroupBy(q =&gt; q.Code)</c>. A shared Rules/Signs question's <see cref="Code"/>
    /// is the combination value (e.g. <c>Code1|Code2</c>), which fails both <c>== Code1</c> and
    /// <c>== Code2</c> equality checks, and would form its own separate <c>Code1|Code2</c> group
    /// under a plain <c>GroupBy</c> instead of folding into both Code1's and Code2's results.
    /// <c>HasFlag</c> correctly matches such a question against every constituent code it applies
    /// to when computing that code's own results.
    /// </para>
    /// </summary>
    public LicenceCode Code { get; set; }

    /// <summary>
    /// Globally sequential position across the whole attempt (1..N): section order is fixed
    /// (Rules -&gt; Signs -&gt; VehicleControls), shuffled only within each section's block. A single
    /// monotonic field is simpler for both storage and client iteration than a compound
    /// per-section counter, and still lets the client group by Section for "Section N of M"
    /// display. Immutable once written - a resume re-reads this value, never re-randomises.
    /// </summary>
    public int DisplayOrder { get; set; }

    public string Stem { get; set; } = string.Empty;

    public string? SignRef { get; set; }

    public List<AttemptAnswerOption> AttemptAnswerOptions { get; set; } = new();
}
