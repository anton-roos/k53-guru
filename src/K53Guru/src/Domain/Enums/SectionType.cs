namespace K53Guru.Domain.Enums;

/// <summary>
/// The fixed test section a <see cref="Entities.Question"/> belongs to.
/// Section order is fixed Rules -> Signs -> VehicleControls; randomisation is intra-section only.
/// </summary>
public enum SectionType
{
    Rules,
    Signs,
    VehicleControls
}
