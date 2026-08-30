using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Tests.Commands.AddEdit;

/// <summary>
/// Field-level rejection that runs before SaveChangesAsync. Every rule attaches to a specific
/// property so the Admin UI can identify the failing field.
/// </summary>
public class AddEditTestCommandValidator : AbstractValidator<AddEditTestCommand>
{
    private const LicenceCode AllKnownCodes = LicenceCode.Code1 | LicenceCode.Code2 | LicenceCode.Code3;

    private const TestSectionScope AllKnownSections =
        TestSectionScope.Rules | TestSectionScope.Signs | TestSectionScope.VehicleControls;

    private readonly IApplicationDbContextFactory _dbContextFactory;

    public AddEditTestCommandValidator(IApplicationDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;

        RuleFor(v => v.Name)
            .NotEmpty()
            .WithMessage("Name is required.");

        // Rejects both LicenceCode.None (zero bits) and any bit outside the three known codes.
        RuleFor(v => v.Codes)
            .Must(c => c != LicenceCode.None && (c & ~AllKnownCodes) == LicenceCode.None)
            .WithMessage("Codes must be a non-empty combination of Code1, Code2, and Code3.");

        // Rejects both TestSectionScope.None (zero bits) and any bit outside the three known
        // sections.
        RuleFor(v => v.Sections)
            .Must(s => s != TestSectionScope.None && (s & ~AllKnownSections) == TestSectionScope.None)
            .WithMessage("Sections must be a non-empty combination of Rules, Signs, and VehicleControls.");

        RuleFor(v => v.QuestionIds)
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("At least one question must be selected.");

        // Mirrors AddEditQuestionCommandValidator's SignRef -> RoadSign resolution rule: every
        // submitted id must reference a real, currently-existing Question row. A stale id (e.g.
        // from a race with another admin, or any non-UI caller) is rejected here with a clean
        // field-attributed message rather than surfacing as a raw DbUpdateException FK violation
        // at SaveChangesAsync.
        RuleFor(v => v.QuestionIds)
            .MustAsync(AllQuestionIdsExistAsync)
            .WithMessage("One or more selected questions no longer exist.");
    }

    private async Task<bool> AllQuestionIdsExistAsync(List<int> questionIds, CancellationToken cancellationToken)
    {
        // The non-empty check above already reports an empty/null list - nothing further to
        // resolve here.
        if (questionIds is not { Count: > 0 }) return true;

        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        var distinctIds = questionIds.Distinct().ToList();
        var matchCount = await db.Questions.CountAsync(q => distinctIds.Contains(q.Id), cancellationToken);
        return matchCount == distinctIds.Count;
    }
}
