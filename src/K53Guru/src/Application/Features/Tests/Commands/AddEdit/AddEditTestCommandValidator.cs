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

    public AddEditTestCommandValidator()
    {
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
    }
}
