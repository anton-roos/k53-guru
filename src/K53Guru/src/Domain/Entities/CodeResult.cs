using K53Guru.Domain.Common.Entities;
using K53Guru.Domain.Enums;

namespace K53Guru.Domain.Entities;

/// <summary>
/// One constituent code's graded outcome within an <see cref="Attempt"/>, produced by
/// SubmitAttemptCommand (Story 3.5). A single-code Attempt yields exactly one CodeResult; a
/// combination Attempt (Story 3.4) yields one per constituent code, each graded independently -
/// a partial pass across codes is possible.
/// </summary>
public class CodeResult : BaseAuditableEntity
{
    public int AttemptId { get; set; }

    /// <summary>
    /// The single code (Code1/Code2/Code3) this result covers - matching
    /// <see cref="AttemptQuestion.Code"/>'s per-constituent-code semantics from
    /// LicenceCodeExtensions.GetConstituentCodes(), never the whole combination value.
    /// </summary>
    public LicenceCode Code { get; set; }

    /// <summary>
    /// True only if every <see cref="SectionResult.Passed"/> under this code is true - failing
    /// any one section fails the whole code.
    /// </summary>
    public bool Passed { get; set; }

    public List<SectionResult> SectionResults { get; set; } = new();
}
