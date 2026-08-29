using K53Guru.Domain.Common.Entities;

namespace K53Guru.Domain.Entities;

public class RoadSign : BaseAuditableEntity
{
    public string LegislationCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageAssetKey { get; set; }
}
