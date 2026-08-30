namespace K53Guru.Application.Features.Tests.Specifications;
#nullable disable warnings
/// <summary>
/// Specification class for keyword-searching Tests.
/// Matches Name only (case-sensitivity follows provider defaults), mirroring
/// QuestionAdvancedSpecification's search style.
/// </summary>
public class TestAdvancedSpecification : Specification<Test>
{
    public TestAdvancedSpecification(PaginationFilter filter)
    {
        Query.Where(
            x => x.Name.Contains(filter.Keyword),
            !string.IsNullOrEmpty(filter.Keyword));
    }
}
