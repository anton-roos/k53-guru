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
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
        {
            foreach (var header in QuestionImportCsvColumns.AllHeaders())
            {
                csv.WriteField(header);
            }
            csv.NextRecord();

            // One filled-in example row - options 3 and 4 left blank (CSV caps options at 4 via
            // fixed columns; unused slots are blank).
            csv.WriteField("What does this sign mean?");
            csv.WriteField("Code1;Code2");
            csv.WriteField(nameof(SectionType.Signs));
            csv.WriteField("en");
            csv.WriteField("R1");
            csv.WriteField("Stop");
            csv.WriteField("true");
            csv.WriteField("Yield");
            csv.WriteField("false");
            csv.WriteField(string.Empty);
            csv.WriteField(string.Empty);
            csv.WriteField(string.Empty);
            csv.WriteField(string.Empty);
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
