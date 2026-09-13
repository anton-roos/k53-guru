using K53Guru.Application.Common.Constants;

namespace K53Guru.Application.Features.RoadSigns.Commands.AddEdit;

/// <summary>
/// Field-level rejection that runs before SaveChangesAsync, mirroring
/// AddEditQuestionCommandValidator's shape exactly (spec-1-4-author-combination-signs.md).
/// </summary>
public class AddEditRoadSignCommandValidator : AbstractValidator<AddEditRoadSignCommand>
{
    /// <summary>
    /// Server-side mirror of RoadSignFormDialog.razor's MudFileUpload Accept list - a client
    /// Accept attribute is a hint only and trivially bypassed via a direct MediatR request.
    /// </summary>
    private static readonly string[] AllowedImageExtensions = { ".svg", ".png", ".jpg", ".jpeg", ".webp" };

    private readonly IApplicationDbContextFactory _dbContextFactory;

    public AddEditRoadSignCommandValidator(IApplicationDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;

        RuleFor(v => v.LegislationCode)
            .NotEmpty()
            .WithMessage("Legislation code is required.")
            .MaximumLength(20)
            .WithMessage("Legislation code must be 20 characters or fewer.");

        RuleFor(v => v.Description)
            .NotEmpty()
            .WithMessage("Description is required.")
            .MaximumLength(500)
            .WithMessage("Description must be 500 characters or fewer.");

        RuleFor(v => v.ComponentSignCodes)
            .MaximumLength(200)
            .WithMessage("Component sign codes must be 200 characters or fewer.");

        // Mirrors AddEditQuestionCommandValidator's SignRef MustAsync rule: LegislationCode
        // uniqueness (excluding self on edit) is validated here, not just via the DB unique
        // index, so the Admin UI can identify the failing field before SaveChangesAsync throws.
        // Compared case-insensitively (and trimmed) because RoadSignImageStore derives its
        // on-disk file name by lowercasing the code - "R3+R531" and "r3+r531" must not be
        // allowed to coexist and silently overwrite each other's image file.
        RuleFor(v => v.LegislationCode)
            .MustAsync(BeUniqueLegislationCodeAsync)
            .WithMessage(v => $"Legislation code '{v.LegislationCode}' already exists.")
            .When(v => !string.IsNullOrWhiteSpace(v.LegislationCode));

        // Ask-First resolution (spec-1-4-author-combination-signs.md): an unresolved component
        // code is hard-rejected, matching the SignRef precedent above, rather than accepted as
        // non-authoritative provenance. Every comma-separated entry must resolve to exactly one
        // RoadSign.LegislationCode - other than the row being edited itself (a composite cannot
        // legitimately list its own LegislationCode as one of its own components).
        RuleFor(v => v.ComponentSignCodes)
            .MustAsync(AllComponentCodesResolveAsync)
            .WithMessage("One or more component sign codes do not match an existing road sign, or reference the sign itself.")
            .When(v => !string.IsNullOrWhiteSpace(v.ComponentSignCodes));

        // Server-side mirror of GlobalVariables.MaxAllowedSize - UserFormDialog.razor-style
        // upload dialogs only cap this client-side via OpenReadStream's maxAllowedSize argument,
        // which is trivially bypassed via a direct MediatR request.
        RuleFor(v => v.ImageBytes)
            .Must(bytes => bytes == null || bytes.LongLength <= GlobalVariables.MaxAllowedSize)
            .WithMessage($"Image must be {GlobalVariables.MaxAllowedSize / (1024 * 1024)}MB or smaller.");

        // Server-side mirror of RoadSignFormDialog.razor's MudFileUpload Accept list - see
        // AllowedImageExtensions above.
        RuleFor(v => v.ImageFileName)
            .Must(fileName => AllowedImageExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Image file must be one of: {string.Join(", ", AllowedImageExtensions)}.")
            .When(v => v.ImageBytes is { Length: > 0 });
    }

    private async Task<bool> BeUniqueLegislationCodeAsync(
        AddEditRoadSignCommand command,
        string? legislationCode,
        CancellationToken cancellationToken)
    {
        var normalizedCode = legislationCode?.Trim().ToUpper();

        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        return !await db.RoadSigns.AnyAsync(
            r => r.LegislationCode.ToUpper() == normalizedCode && r.Id != command.Id,
            cancellationToken);
    }

    private async Task<bool> AllComponentCodesResolveAsync(
        AddEditRoadSignCommand command,
        string? componentSignCodes,
        CancellationToken cancellationToken)
    {
        var codes = SplitCodes(componentSignCodes);
        if (codes.Count == 0) return true;

        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);

        // Exclude the row being edited from the candidate set: self-reference must never
        // "resolve", even though the row's own LegislationCode otherwise matches itself.
        var matchCount = await db.RoadSigns
            .Where(r => r.Id != command.Id)
            .CountAsync(r => codes.Contains(r.LegislationCode), cancellationToken);
        return matchCount == codes.Count;
    }

    private static List<string> SplitCodes(string? componentSignCodes)
    {
        return string.IsNullOrWhiteSpace(componentSignCodes)
            ? new List<string>()
            : componentSignCodes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();
    }
}
