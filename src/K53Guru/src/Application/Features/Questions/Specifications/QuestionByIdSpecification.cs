namespace K53Guru.Application.Features.Questions.Specifications;
#nullable disable warnings
/// <summary>
/// Specification class for filtering Questions by their ID, including AnswerOptions so the
/// edit dialog gets the full child list.
/// </summary>
public class QuestionByIdSpecification : Specification<Question>
{
    public QuestionByIdSpecification(int id)
    {
        Query.Where(q => q.Id == id)
             .Include(q => q.AnswerOptions);
    }
}
