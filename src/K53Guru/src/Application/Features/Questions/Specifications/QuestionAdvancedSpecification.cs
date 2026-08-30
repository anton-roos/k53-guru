namespace K53Guru.Application.Features.Questions.Specifications;
#nullable disable warnings
/// <summary>
/// Specification class for keyword-searching Questions.
/// Matches Stem only (case-sensitivity follows provider defaults), mirroring
/// RoadSignAdvancedSpecification/ProductAdvancedSpecification's search style.
/// </summary>
public class QuestionAdvancedSpecification : Specification<Question>
{
    public QuestionAdvancedSpecification(PaginationFilter filter)
    {
        Query.Where(
            x => x.Stem.Contains(filter.Keyword),
            !string.IsNullOrEmpty(filter.Keyword));
    }
}
