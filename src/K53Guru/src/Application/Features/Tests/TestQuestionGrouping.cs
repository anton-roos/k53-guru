using K53Guru.Application.Features.Questions.DTOs;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Tests;

/// <summary>
/// Pure grouping/counting logic for the "view" acceptance criterion: a Test's associated
/// questions grouped by section (Rules -> Signs -> Controls) with per-section and per-code
/// counts. Lives here (not inlined in TestFormDialog.razor's code-behind) so both the Razor
/// component and unit tests exercise the exact same implementation - a regression here fails a
/// test instead of shipping silently.
/// </summary>
public static class TestQuestionGrouping
{
    public static List<SectionQuestionGroup> GroupBySectionWithCodeCounts(IEnumerable<QuestionDto> questions)
    {
        return questions
            .GroupBy(q => q.Section)
            .OrderBy(g => (int)g.Key)
            .Select(g => new SectionQuestionGroup
            {
                Section = g.Key,
                Count = g.Count(),
                CodeCounts = CountByCode(g)
            })
            .ToList();
    }

    /// <summary>
    /// Per-code question counts within one section group. A question may carry more than one
    /// code, so it is counted once per matching code. Codes with zero matches are omitted.
    /// </summary>
    private static List<CodeQuestionCount> CountByCode(IEnumerable<QuestionDto> questions)
    {
        var list = questions.ToList();
        var counts = new List<CodeQuestionCount>();
        foreach (var flag in new[] { LicenceCode.Code1, LicenceCode.Code2, LicenceCode.Code3 })
        {
            var count = list.Count(q => q.Codes.HasFlag(flag));
            if (count > 0)
            {
                counts.Add(new CodeQuestionCount { Code = flag.ToString(), Count = count });
            }
        }

        return counts;
    }
}

public class SectionQuestionGroup
{
    public SectionType Section { get; set; }
    public int Count { get; set; }
    public List<CodeQuestionCount> CodeCounts { get; set; } = new();
}

public class CodeQuestionCount
{
    public string Code { get; set; } = string.Empty;
    public int Count { get; set; }
}
