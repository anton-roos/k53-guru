using K53Guru.Domain.Enums;

namespace K53Guru.Domain.Common;

/// <summary>
/// Shared <see cref="LicenceCode"/> flags-enum helpers. Extracted from StartAttemptCommand's
/// original inline `if (test.Codes.HasFlag(...))` checks (Story 3.4) so SubmitAttemptCommand
/// (Story 3.5) can derive the same constituent-code decomposition without duplicating that logic.
/// </summary>
public static class LicenceCodeExtensions
{
    /// <summary>
    /// Decomposes a <see cref="LicenceCode"/> value - a single code or a valid combination - into
    /// its constituent codes, in fixed composition order: Code1 first when present, then Code2,
    /// then Code3. For a single code this returns a one-element list containing just that code.
    /// </summary>
    public static List<LicenceCode> GetConstituentCodes(this LicenceCode codes)
    {
        var constituentCodes = new List<LicenceCode>();
        if (codes.HasFlag(LicenceCode.Code1)) constituentCodes.Add(LicenceCode.Code1);
        if (codes.HasFlag(LicenceCode.Code2)) constituentCodes.Add(LicenceCode.Code2);
        if (codes.HasFlag(LicenceCode.Code3)) constituentCodes.Add(LicenceCode.Code3);
        return constituentCodes;
    }
}
