namespace K53Guru.Application.Features.RoadSigns.Specifications;
#nullable disable warnings
/// <summary>
/// Specification class for keyword-searching the RoadSign catalog.
/// Matches LegislationCode or Description (case-sensitivity follows provider defaults),
/// mirroring ProductAdvancedSpecification/PicklistSetAdvancedSpecification's search style.
/// </summary>
public class RoadSignAdvancedSpecification : Specification<RoadSign>
{
    public RoadSignAdvancedSpecification(PaginationFilter filter)
    {
        Query.Where(
            x => x.LegislationCode.Contains(filter.Keyword) || x.Description.Contains(filter.Keyword),
            !string.IsNullOrEmpty(filter.Keyword));
    }
}
