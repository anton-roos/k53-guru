using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Questions.Commands.AddEdit;

/// <summary>
/// Field-level rejection that runs before SaveChangesAsync, complementing (not replacing)
/// QuestionValidationInterceptor's SaveChangesAsync-time safety net (Story 1.3). Every rule
/// attaches to a specific property so the Admin UI can identify the failing field.
/// </summary>
public class AddEditQuestionCommandValidator : AbstractValidator<AddEditQuestionCommand>
{
    private const LicenceCode AllKnownCodes = LicenceCode.Code1 | LicenceCode.Code2 | LicenceCode.Code3;

    private readonly IApplicationDbContextFactory _dbContextFactory;

    public AddEditQuestionCommandValidator(IApplicationDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;

        RuleFor(v => v.Stem)
            .NotEmpty()
            .WithMessage("Stem is required.");

        // Rejects both LicenceCode.None (zero bits) and any bit outside the three known codes
        // (e.g. a malformed direct API/import call - unreachable through the UI's checkbox-style
        // selector, but not through the command itself).
        RuleFor(v => v.Codes)
            .Must(c => c != LicenceCode.None && (c & ~AllKnownCodes) == LicenceCode.None)
            .WithMessage("Codes must be a non-empty combination of Code1, Code2, and Code3.");

        RuleFor(v => v.AnswerOptions)
            .Must(options => options is { Count: >= 2 })
            .WithMessage("At least two answer options are required.");

        RuleFor(v => v.AnswerOptions)
            .Must(options => options != null && options.Count(a => a.IsCorrect) == 1)
            .WithMessage("Exactly one answer option must be marked as correct.");

        RuleForEach(v => v.AnswerOptions)
            .ChildRules(option =>
            {
                option.RuleFor(a => a.Text)
                    .NotEmpty()
                    .WithMessage("Answer option text is required.");
            });

        // This codebase's first MustAsync rule: resolves SignRef against the RoadSign catalog.
        // A non-empty SignRef must resolve to exactly one RoadSign.LegislationCode - zero matches
        // (unresolved) or more than one (ambiguous) are both a hard rejection.
        RuleFor(v => v.SignRef)
            .MustAsync(SignRefResolvesToExactlyOneRoadSignAsync)
            .WithMessage(v => $"Sign reference '{v.SignRef}' does not resolve to exactly one road sign.");
    }

    private async Task<bool> SignRefResolvesToExactlyOneRoadSignAsync(string? signRef, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(signRef)) return true;

        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        var matchCount = await db.RoadSigns.CountAsync(r => r.LegislationCode == signRef, cancellationToken);
        return matchCount == 1;
    }
}
