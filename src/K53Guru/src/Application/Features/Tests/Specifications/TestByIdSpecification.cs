namespace K53Guru.Application.Features.Tests.Specifications;
#nullable disable warnings
/// <summary>
/// Specification class for filtering Tests by their ID.
/// </summary>
public class TestByIdSpecification : Specification<Test>
{
    public TestByIdSpecification(int id)
    {
        Query.Where(t => t.Id == id);
    }
}
