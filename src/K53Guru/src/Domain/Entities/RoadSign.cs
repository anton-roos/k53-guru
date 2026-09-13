using K53Guru.Domain.Common.Entities;

namespace K53Guru.Domain.Entities;

public class RoadSign : BaseAuditableEntity
{
    public string LegislationCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageAssetKey { get; set; }

    /// <summary>
    /// Admin-display provenance only (SPEC.md CAP-10): a comma-separated list of the existing
    /// catalog LegislationCodes this composite was assembled from (e.g. "R3,R531"). Never a
    /// normalized relationship or JSON list - the simplest shape that round-trips through the
    /// existing AutoMapper projections. Never read by the learner API, AttemptQuestion, or the
    /// Flutter client - a plain sign (not a composite) leaves this null.
    /// </summary>
    public string? ComponentSignCodes { get; set; }
}
