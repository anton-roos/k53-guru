namespace K53Guru.Application.Features.RoadSigns.Specifications;
#nullable disable warnings
/// <summary>
/// Specification class for filtering RoadSigns by their ID.
/// </summary>
public class RoadSignByIdSpecification : Specification<RoadSign>
{
    public RoadSignByIdSpecification(int id)
    {
        Query.Where(q => q.Id == id);
    }
}
