using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using K53Guru.Application.Features.Questions.Caching;
using K53Guru.Application.Features.Questions.Commands.AddEdit;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Questions.Commands.Import;

public class ImportQuestionsCommand : ICacheInvalidatorRequest<Result>
{
    public ImportQuestionsCommand(string fileName, byte[] data)
    {
        FileName = fileName;
        Data = data;
    }

    public string FileName { get; set; }
    public byte[] Data { get; set; }

    public string CacheKey => QuestionCacheKey.GetAllCacheKey;
    public IEnumerable<string>? Tags => QuestionCacheKey.Tags;
}

/// <summary>
/// Parses a CSV or JSON question bank (format detected by <see cref="ImportQuestionsCommand.FileName"/>
/// extension) and imports it with reject-on-error, all-or-nothing semantics (spec-2-4).
///
/// Two-phase, matching "no partial content is stored":
///  - Phase 1 parses every row into a format-agnostic <see cref="QuestionImportRow"/> (a per-row
///    parse exception is caught and folded into the failure list rather than aborting the whole
///    read), builds an AddEditQuestionCommand-shaped object per successfully-parsed row, and runs
///    it through the *exact same* <see cref="IValidator{AddEditQuestionCommand}"/> Story 2.1
///    built (injected via DI - FluentValidation registers AddEditQuestionCommandValidator for
///    this interface, so there is zero duplicated validation logic). Every failure across every
///    row is collected - not just the first - before this handler decides anything. The
///    persistence DbContext is not created at all during this phase.
///  - Phase 2 (only reached if phase 1 found zero failures across every row) builds every
///    Question+AnswerOption entity - mirroring AddEditQuestionCommandHandler's create-branch
///    entity-building shape inline, not via a shared method, matching this codebase's preference
///    for small explicit duplication over premature abstraction (see PublishTestCommand /
///    UnpublishTestCommand) - and calls SaveChangesAsync exactly once.
///
/// Reusing AddEditQuestionCommandValidator per row means its MustAsync SignRef rule issues one DB
/// round-trip per row - the N+1 pattern flagged in spec-1-3-question-content-model.md's deferred
/// work as relevant here. Consciously accepted for v1's expected import volumes (tens to low
/// hundreds of rows), not batch-optimized now.
/// </summary>
public class ImportQuestionsCommandHandler : IRequestHandler<ImportQuestionsCommand, Result>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IValidator<AddEditQuestionCommand> _questionValidator;

    public ImportQuestionsCommandHandler(
        IApplicationDbContextFactory dbContextFactory,
        IValidator<AddEditQuestionCommand> questionValidator
    )
    {
        _dbContextFactory = dbContextFactory;
        _questionValidator = questionValidator;
    }

    public async Task<Result> Handle(ImportQuestionsCommand request, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        var failures = new List<string>();

        List<(int Position, QuestionImportRow? Row)> parsedRows;
        switch (extension)
        {
            case ".csv":
                parsedRows = ParseCsv(request.Data, failures);
                break;
            case ".json":
                parsedRows = ParseJson(request.Data, failures);
                break;
            default:
                return await Result.FailureAsync(
                    $"Unsupported file type '{extension}'. Only .csv and .json files can be imported.");
        }

        // Phase 1: validate every row through the exact same validator Story 2.1's dialog uses.
        // Every failure - parse or validation - across every row is collected before any
        // decision is made; the DbContext is not touched at all in this phase.
        var toSave = new List<AddEditQuestionCommand>();
        foreach (var (position, row) in parsedRows)
        {
            if (row is null) continue; // parsing already failed for this row (recorded above).

            AddEditQuestionCommand command;
            try
            {
                command = BuildCommand(row);
            }
            catch (Exception ex)
            {
                failures.Add($"Row {position}: {ex.Message}");
                continue;
            }

            var validationResult = await _questionValidator.ValidateAsync(command, cancellationToken);
            if (!validationResult.IsValid)
            {
                failures.AddRange(validationResult.Errors.Select(e => $"Row {position}: {e.ErrorMessage}"));
                continue;
            }

            toSave.Add(command);
        }

        if (failures.Count > 0)
        {
            return await Result.FailureAsync(failures.ToArray());
        }

        // Phase 2: every row passed - build and save all entities in one SaveChangesAsync.
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        foreach (var command in toSave)
        {
            db.Questions.Add(new Question
            {
                Stem = command.Stem ?? string.Empty,
                Codes = command.Codes,
                Section = command.Section,
                LanguageCode = command.LanguageCode,
                SignRef = command.SignRef,
                AnswerOptions = BuildAnswerOptions(command.AnswerOptions)
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return await Result.SuccessAsync();
    }

    /// <summary>
    /// Converts a format-agnostic parsed row into the exact shape AddEditQuestionCommandValidator
    /// validates. Throws (caught by the caller and folded into the row's failure) when a Codes
    /// token or the Section value cannot be parsed to their enums - a genuinely malformed cell,
    /// as opposed to a merely empty/invalid *value* for a known field, which the reused validator
    /// itself rejects (e.g. empty Stem, empty Codes, wrong correct-answer count).
    /// </summary>
    private static AddEditQuestionCommand BuildCommand(QuestionImportRow row)
    {
        var codes = LicenceCode.None;
        foreach (var code in row.Codes)
        {
            codes |= Enum.Parse<LicenceCode>(code, ignoreCase: true);
        }

        var section = Enum.Parse<SectionType>(row.Section ?? string.Empty, ignoreCase: true);

        return new AddEditQuestionCommand
        {
            Stem = row.Stem,
            Codes = codes,
            Section = section,
            LanguageCode = string.IsNullOrWhiteSpace(row.LanguageCode) ? "en" : row.LanguageCode,
            SignRef = row.SignRef,
            AnswerOptions = row.AnswerOptions
                .Select(a => new AnswerOptionModel { Text = a.Text, IsCorrect = a.IsCorrect })
                .ToList()
        };
    }

    /// <summary>
    /// Mirrors AddEditQuestionCommandHandler.BuildAnswerOptions exactly: Order is always the
    /// option's position in the list, never imported data.
    /// </summary>
    private static List<AnswerOption> BuildAnswerOptions(List<AnswerOptionModel> submitted)
    {
        var options = new List<AnswerOption>();
        for (var index = 0; index < submitted.Count; index++)
        {
            var model = submitted[index];
            options.Add(new AnswerOption
            {
                Text = model.Text ?? string.Empty,
                IsCorrect = model.IsCorrect,
                Order = index
            });
        }

        return options;
    }

    /// <summary>
    /// Parses the CSV file row-by-row, catching a parse exception on any single row (bad boolean
    /// value, etc.) and continuing to the next row rather than aborting the whole read.
    /// </summary>
    private static List<(int Position, QuestionImportRow? Row)> ParseCsv(byte[] data, List<string> failures)
    {
        var rows = new List<(int, QuestionImportRow?)>();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var stream = new MemoryStream(data);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var csv = new CsvReader(reader, config);

        if (!csv.Read())
        {
            return rows;
        }

        csv.ReadHeader();

        var position = 0;
        while (true)
        {
            bool hasRecord;
            try
            {
                hasRecord = csv.Read();
            }
            catch (Exception ex)
            {
                failures.Add($"Row {position + 1}: {ex.Message}");
                break;
            }

            if (!hasRecord) break;

            position++;
            try
            {
                rows.Add((position, ParseCsvRow(csv)));
            }
            catch (Exception ex)
            {
                failures.Add($"Row {position}: {ex.Message}");
                rows.Add((position, null));
            }
        }

        return rows;
    }

    private static QuestionImportRow ParseCsvRow(CsvReader csv)
    {
        string Field(string name)
        {
            csv.TryGetField<string>(name, out var value);
            return value ?? string.Empty;
        }

        var languageCode = Field(QuestionImportCsvColumns.LanguageCode);
        var signRef = Field(QuestionImportCsvColumns.SignRef);

        var row = new QuestionImportRow
        {
            Stem = Field(QuestionImportCsvColumns.Stem),
            Codes = Field(QuestionImportCsvColumns.Codes)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            Section = Field(QuestionImportCsvColumns.Section),
            LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode,
            SignRef = string.IsNullOrWhiteSpace(signRef) ? null : signRef,
            AnswerOptions = new List<(string, bool)>()
        };

        for (var i = 1; i <= QuestionImportCsvColumns.MaxAnswerOptions; i++)
        {
            var text = Field(QuestionImportCsvColumns.OptionText(i));
            if (string.IsNullOrWhiteSpace(text)) continue; // unused slot - CSV caps options at 4.

            var isCorrect = ParseBool(Field(QuestionImportCsvColumns.OptionCorrect(i)));
            row.AnswerOptions.Add((text, isCorrect));
        }

        return row;
    }

    private static bool ParseBool(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var trimmed = raw.Trim();
        if (bool.TryParse(trimmed, out var parsed)) return parsed;
        if (trimmed == "1") return true;
        if (trimmed == "0") return false;
        throw new FormatException($"Invalid boolean value '{raw}'.");
    }

    /// <summary>
    /// Parses the JSON array element-by-element, catching a parse exception on any single element
    /// (a field with the wrong JSON type, etc.) and continuing to the next rather than aborting
    /// the whole read. A malformed top-level document (not valid JSON, or not an array) is a
    /// whole-file failure since there is no row to recover to.
    /// </summary>
    private static List<(int Position, QuestionImportRow? Row)> ParseJson(byte[] data, List<string> failures)
    {
        var rows = new List<(int, QuestionImportRow?)>();

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(data);
        }
        catch (JsonException ex)
        {
            failures.Add($"Row 1: {ex.Message}");
            return rows;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                failures.Add("Row 1: JSON root must be an array of questions.");
                return rows;
            }

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var position = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                position++;
                try
                {
                    rows.Add((position, ParseJsonRow(element, jsonOptions)));
                }
                catch (Exception ex)
                {
                    failures.Add($"Row {position}: {ex.Message}");
                    rows.Add((position, null));
                }
            }
        }

        return rows;
    }

    private static QuestionImportRow ParseJsonRow(JsonElement element, JsonSerializerOptions options)
    {
        var dto = element.Deserialize<QuestionImportRowJson>(options)
                  ?? throw new JsonException("Row is empty.");

        return new QuestionImportRow
        {
            Stem = dto.Stem,
            Codes = dto.Codes ?? new List<string>(),
            Section = dto.Section,
            LanguageCode = string.IsNullOrWhiteSpace(dto.LanguageCode) ? "en" : dto.LanguageCode,
            SignRef = dto.SignRef,
            AnswerOptions = (dto.AnswerOptions ?? new List<QuestionImportAnswerOptionJson>())
                .Select(a => (a.Text ?? string.Empty, a.IsCorrect))
                .ToList()
        };
    }

    private class QuestionImportRowJson
    {
        public string? Stem { get; set; }
        public List<string>? Codes { get; set; }
        public string? Section { get; set; }
        public string? LanguageCode { get; set; }
        public string? SignRef { get; set; }
        public List<QuestionImportAnswerOptionJson>? AnswerOptions { get; set; }
    }

    private class QuestionImportAnswerOptionJson
    {
        public string? Text { get; set; }
        public bool IsCorrect { get; set; }
    }
}
