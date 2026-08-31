using K53Guru.Domain.Common.Entities;

namespace K53Guru.Domain.Entities;

/// <summary>
/// A minimal, anonymous learner identity - no accounts, no PII. The client generates its own
/// UUID locally (no server-side "register" step) and first uses it when it starts its first
/// <see cref="Attempt"/> (see StartAttemptCommand, which upserts this row).
///
/// This is the solution's first Guid-keyed entity, scoped narrowly to the one entity that
/// genuinely needs a client-held identifier with no server round-trip. It implements
/// <see cref="IAuditableEntity"/> directly rather than inheriting <see cref="BaseAuditableEntity"/>,
/// which is hard-wired to <c>int</c> via <c>BaseEntity : IEntity&lt;int&gt;</c>.
/// </summary>
public class LearnerProfile : IAuditableEntity
{
    /// <summary>
    /// Client-supplied UUID - never server-generated. Configured with ValueGeneratedNever() (see
    /// LearnerProfileConfiguration) so EF never overwrites a client-supplied value.
    /// </summary>
    public Guid Id { get; set; }

    public DateTime? CreatedAt { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string? LastModifiedById { get; set; }

    public List<Attempt> Attempts { get; set; } = new();
}
