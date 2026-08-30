using K53Guru.Domain.Common.Entities;
using K53Guru.Domain.Enums;

namespace K53Guru.Domain.Entities;

public class Question : BaseAuditableEntity
{
    public string Stem { get; set; } = string.Empty;
    public LicenceCode Codes { get; set; }
    public SectionType Section { get; set; }
    public string LanguageCode { get; set; } = "en";

    /// <summary>
    /// The official legislation-code string of the referenced <see cref="RoadSign"/>.
    /// Never an FK - resolved to exactly one catalog sign at save time.
    /// </summary>
    public string? SignRef { get; set; }

    public List<AnswerOption> AnswerOptions { get; set; } = new();
}
