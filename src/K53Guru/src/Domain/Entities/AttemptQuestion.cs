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
