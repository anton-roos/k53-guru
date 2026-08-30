namespace K53Guru.Application.Features.Questions.Commands.Import;

/// <summary>
/// Shallow command-level validator - mirrors ImportPicklistSetsCommandValidator.cs. The real,
/// per-row validation happens inside ImportQuestionsCommandHandler via the reused
/// AddEditQuestionCommandValidator; this only guards against an empty/missing upload.
/// </summary>
public class ImportQuestionsCommandValidator : AbstractValidator<ImportQuestionsCommand>
{
    public ImportQuestionsCommandValidator()
    {
        RuleFor(x => x.Data).NotNull().NotEmpty();
    }
}
