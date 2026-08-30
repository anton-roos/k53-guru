namespace K53Guru.Application.Features.Tests.Specifications;
#nullable disable warnings
/// <summary>
/// Specification class for filtering Tests by their ID, including the TestQuestions join rows
/// and each row's Question so the grouped-view panel gets the full associated-question list.
/// </summary>
public class TestByIdSpecification : Specification<Test>
{
    public TestByIdSpecification(int id)
    {
        Query.Where(t => t.Id == id)
             .Include(t => t.TestQuestions)
             .ThenInclude(tq => tq.Question);
    }
}
