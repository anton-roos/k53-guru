namespace K53Guru.Application.Features.Questions.Commands.Import;

/// <summary>
/// Format-agnostic row produced by both the CSV and JSON parsers in
/// <see cref="ImportQuestionsCommandHandler"/>. Phase-1 validation and phase-2 entity building
/// operate purely on this shape, so everything past the parse step is identical regardless of
/// whether the source file was CSV or JSON.
/// </summary>
public class QuestionImportRow
{
    public string? Stem { get; set; }
    public List<string> Codes { get; set; } = new();
    public string? Section { get; set; }
    public string LanguageCode { get; set; } = "en";
    public string? SignRef { get; set; }
    public List<(string Text, bool IsCorrect)> AnswerOptions { get; set; } = new();
}

/// <summary>
/// CSV column headers for the question import/template row shape. Answer options are capped at
/// 4 via fixed columns (blank for unused slots) because CSV has no native array shape; JSON has
/// no such cap (<see cref="ImportQuestionsCommandHandler"/>'s JSON row DTO uses a plain array).
/// Shared by <see cref="ImportQuestionsCommandHandler"/> (reads) and
/// <see cref="CreateQuestionsImportTemplateCommandHandler"/> (writes) so the two never drift.
/// </summary>
public static class QuestionImportCsvColumns
{
    public const string Stem = "Stem";
    public const string Codes = "Codes";
    public const string Section = "Section";
    public const string LanguageCode = "LanguageCode";
    public const string SignRef = "SignRef";

    public const int MaxAnswerOptions = 4;

    public static string OptionText(int index) => $"Option{index}Text";
    public static string OptionCorrect(int index) => $"Option{index}Correct";

    public static IEnumerable<string> AllHeaders()
    {
        yield return Stem;
        yield return Codes;
        yield return Section;
        yield return LanguageCode;
        yield return SignRef;
        for (var i = 1; i <= MaxAnswerOptions; i++)
        {
            yield return OptionText(i);
            yield return OptionCorrect(i);
        }
    }
}
