using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Questions.Commands.Import;

/// <summary>
/// Returns a downloadable CSV or JSON template matching the row shape ImportQuestionsCommand
/// parses, so an admin can see the expected columns/fields before filling in a real bank.
/// </summary>
public class CreateQuestionsImportTemplateCommand : IRequest<Result<byte[]>>
{
    /// <summary>"csv" or "json" (case-insensitive).</summary>
    public string Format { get; set; } = "csv";
}

public class CreateQuestionsImportTemplateCommandHandler
    : IRequestHandler<CreateQuestionsImportTemplateCommand, Result<byte[]>>
{
    public async Task<Result<byte[]>> Handle(CreateQuestionsImportTemplateCommand request, CancellationToken cancellationToken)
    {
        return request.Format?.Trim().ToLowerInvariant() switch
        {
            "csv" => await Result<byte[]>.SuccessAsync(BuildCsvTemplate()),
            "json" => await Result<byte[]>.SuccessAsync(BuildJsonTemplate()),
            _ => await Result<byte[]>.FailureAsync(
                $"Unsupported template format '{request.Format}'. Use 'csv' or 'json'.")
        };
    }

    private static byte[] BuildCsvTemplate()
    {
        // Keyed by the same header constants QuestionImportCsvColumns.AllHeaders() yields, so the
        // example row is written by header name below - it can never drift out of alignment with
        // the header row even if the header list changes. Options 3 and 4 are left blank (CSV caps
        // options at 4 via fixed columns; unused slots are blank).
        var exampleRow = new Dictionary<string, string>
        {
            [QuestionImportCsvColumns.Stem] = "What does this sign mean?",
            [QuestionImportCsvColumns.Codes] = "Code1;Code2",
            [QuestionImportCsvColumns.Section] = nameof(SectionType.Signs),
            [QuestionImportCsvColumns.LanguageCode] = "en",
            [QuestionImportCsvColumns.SignRef] = "R1",
            [QuestionImportCsvColumns.OptionText(1)] = "Stop",
            [QuestionImportCsvColumns.OptionCorrect(1)] = "true",
            [QuestionImportCsvColumns.OptionText(2)] = "Yield",
            [QuestionImportCsvColumns.OptionCorrect(2)] = "false",
            [QuestionImportCsvColumns.OptionText(3)] = string.Empty,
            [QuestionImportCsvColumns.OptionCorrect(3)] = string.Empty,
            [QuestionImportCsvColumns.OptionText(4)] = string.Empty,
            [QuestionImportCsvColumns.OptionCorrect(4)] = string.Empty
        };

        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
        {
            var headers = QuestionImportCsvColumns.AllHeaders().ToList();
            foreach (var header in headers)
            {
                csv.WriteField(header);
            }
            csv.NextRecord();

            foreach (var header in headers)
            {
                csv.WriteField(exampleRow.TryGetValue(header, out var value) ? value : string.Empty);
            }
            csv.NextRecord();
        }

        return stream.ToArray();
    }

    private static byte[] BuildJsonTemplate()
    {
        var template = new[]
        {
            new
            {
                stem = "What does this sign mean?",
                codes = new[] { nameof(LicenceCode.Code1), nameof(LicenceCode.Code2) },
                section = nameof(SectionType.Signs),
                languageCode = "en",
                signRef = "R1",
                answerOptions = new object[]
                {
                    new { text = "Stop", isCorrect = true },
                    new { text = "Yield", isCorrect = false }
                }
            }
        };

        var json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
        return Encoding.UTF8.GetBytes(json);
    }
}
