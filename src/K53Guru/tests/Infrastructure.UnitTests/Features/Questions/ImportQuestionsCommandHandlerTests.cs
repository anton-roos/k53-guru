using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using CsvHelper;
using FluentValidation;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.Questions.Commands.AddEdit;
using K53Guru.Application.Features.Questions.Commands.Import;
using K53Guru.Domain.Entities;
using K53Guru.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Features.Questions;

/// <summary>
/// Covers spec-2-4-import-question-bank.md's I/O &amp; Edge-Case Matrix directly against the
/// production ImportQuestionsCommandHandler (which itself drives the real, unmodified
/// AddEditQuestionCommandValidator per row) and CreateQuestionsImportTemplateCommandHandler.
/// Mirrors AddEditQuestionCommandHandlerTests.cs's SQLite in-memory harness rationale: no live
/// MSSQL/PostgreSQL instance is reachable in this sandbox.
///
/// Matrix rows covered:
///   - Import, valid CSV (2 rows persisted)
///   - Import, valid JSON (2 rows persisted)
///   - Import, missing required field (rejected, nothing persisted)
///   - Import, unresolved sign_ref (rejected, nothing persisted)
///   - Import, wrong correct-answer count (rejected, nothing persisted)
///   - Request CSV template
///   - Request JSON template
/// </summary>
public class ImportQuestionsCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ImportQuestionsCommandHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var schemaContext = new ApplicationDbContext(_options);
        schemaContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private IApplicationDbContextFactory CreateFactory()
    {
        var factoryMock = new Mock<IApplicationDbContextFactory>();
        factoryMock
            .Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (IApplicationDbContext)new ApplicationDbContext(_options));
        return factoryMock.Object;
    }

    private ImportQuestionsCommandHandler CreateHandler()
    {
        var validator = new AddEditQuestionCommandValidator(CreateFactory());
        return new ImportQuestionsCommandHandler(CreateFactory(), validator);
    }

    private void SeedRoadSign(string legislationCode)
    {
        using var seedContext = new ApplicationDbContext(_options);
        seedContext.RoadSigns.Add(new RoadSign { LegislationCode = legislationCode, Description = "Stop" });
        seedContext.SaveChanges();
    }

    // Property order matches QuestionImportCsvColumns.AllHeaders() exactly, so CsvWriter's
    // reflection-based header/record writer produces the same shape the handler parses.
    private class CsvRow
    {
        public string Stem { get; set; } = string.Empty;
        public string Codes { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = "en";
        public string SignRef { get; set; } = string.Empty;
        public string Option1Text { get; set; } = string.Empty;
        public string Option1Correct { get; set; } = string.Empty;
        public string Option2Text { get; set; } = string.Empty;
        public string Option2Correct { get; set; } = string.Empty;
        public string Option3Text { get; set; } = string.Empty;
        public string Option3Correct { get; set; } = string.Empty;
        public string Option4Text { get; set; } = string.Empty;
        public string Option4Correct { get; set; } = string.Empty;
    }

    private static byte[] BuildCsv(params CsvRow[] rows)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(rows);
        }

        return stream.ToArray();
    }

    private static byte[] BuildJson(string json) => Encoding.UTF8.GetBytes(json);

    [Fact]
    public async Task Import_ValidCsv_TwoRows_PersistsBothQuestionsWithOptionsAndCorrectAnswers()
    {
        // Arrange
        SeedRoadSign("R1");
        var csv = BuildCsv(
            new CsvRow
            {
                Stem = "What does this sign mean?",
                Codes = "Code1;Code2",
                Section = "Signs",
                LanguageCode = "en",
                SignRef = "R1",
                Option1Text = "Stop",
                Option1Correct = "true",
                Option2Text = "Yield",
                Option2Correct = "false"
            },
            new CsvRow
            {
                Stem = "What is the speed limit in an urban area?",
                Codes = "Code1",
                Section = "Rules",
                LanguageCode = "en",
                Option1Text = "60 km/h",
                Option1Correct = "true",
                Option2Text = "100 km/h",
                Option2Correct = "false"
            });

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportQuestionsCommand("bank.csv", csv), CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);

        await using var verifyContext = new ApplicationDbContext(_options);
        var saved = await verifyContext.Questions.Include(q => q.AnswerOptions)
            .OrderBy(q => q.Id).ToListAsync();
        Assert.Equal(2, saved.Count);

        var signQuestion = saved.Single(q => q.Stem == "What does this sign mean?");
        Assert.Equal("R1", signQuestion.SignRef);
        Assert.Equal(2, signQuestion.AnswerOptions.Count);
        Assert.Single(signQuestion.AnswerOptions, a => a.IsCorrect && a.Text == "Stop");
        Assert.Single(signQuestion.AnswerOptions, a => !a.IsCorrect && a.Text == "Yield");

        var rulesQuestion = saved.Single(q => q.Stem == "What is the speed limit in an urban area?");
        Assert.Null(rulesQuestion.SignRef);
        Assert.Equal(2, rulesQuestion.AnswerOptions.Count);
        Assert.Single(rulesQuestion.AnswerOptions, a => a.IsCorrect && a.Text == "60 km/h");
    }

    [Fact]
    public async Task Import_ValidJson_TwoRows_PersistsBothQuestions()
    {
        // Arrange
        SeedRoadSign("R1");
        var json = """
        [
          {
            "stem": "What does this sign mean?",
            "codes": ["Code1", "Code2"],
            "section": "Signs",
            "languageCode": "en",
            "signRef": "R1",
            "answerOptions": [
              { "text": "Stop", "isCorrect": true },
              { "text": "Yield", "isCorrect": false }
            ]
          },
          {
            "stem": "What is the speed limit in an urban area?",
            "codes": ["Code1"],
            "section": "Rules",
            "languageCode": "en",
            "answerOptions": [
              { "text": "60 km/h", "isCorrect": true },
              { "text": "100 km/h", "isCorrect": false }
            ]
          }
        ]
        """;

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportQuestionsCommand("bank.json", BuildJson(json)), CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);

        await using var verifyContext = new ApplicationDbContext(_options);
        var saved = await verifyContext.Questions.Include(q => q.AnswerOptions).ToListAsync();
        Assert.Equal(2, saved.Count);
        Assert.Contains(saved, q => q.Stem == "What does this sign mean?" && q.SignRef == "R1");
        Assert.Contains(saved, q => q.Stem == "What is the speed limit in an urban area?");
        Assert.All(saved, q => Assert.Single(q.AnswerOptions, a => a.IsCorrect));
    }

    [Fact]
    public async Task Import_MissingRequiredField_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange: row 1 is valid, row 2 has an empty Stem.
        var csv = BuildCsv(
            new CsvRow
            {
                Stem = "Valid question?",
                Codes = "Code1",
                Section = "Rules",
                LanguageCode = "en",
                Option1Text = "Correct",
                Option1Correct = "true",
                Option2Text = "Wrong",
                Option2Correct = "false"
            },
            new CsvRow
            {
                Stem = "",
                Codes = "Code1",
                Section = "Rules",
                LanguageCode = "en",
                Option1Text = "Correct",
                Option1Correct = "true",
                Option2Text = "Wrong",
                Option2Correct = "false"
            });

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportQuestionsCommand("bank.csv", csv), CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.StartsWith("Row 2:", StringComparison.Ordinal));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    [Fact]
    public async Task Import_UnresolvedSignRef_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange: no RoadSign seeded, so "NOPE" cannot resolve. Second, otherwise-valid row
        // proves the whole import - not just the offending row - is rejected.
        var json = """
        [
          {
            "stem": "Valid question?",
            "codes": ["Code1"],
            "section": "Rules",
            "answerOptions": [
              { "text": "Correct", "isCorrect": true },
              { "text": "Wrong", "isCorrect": false }
            ]
          },
          {
            "stem": "What does this unresolved sign mean?",
            "codes": ["Code1"],
            "section": "Signs",
            "signRef": "NOPE",
            "answerOptions": [
              { "text": "Correct", "isCorrect": true },
              { "text": "Wrong", "isCorrect": false }
            ]
          }
        ]
        """;

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportQuestionsCommand("bank.json", BuildJson(json)), CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.StartsWith("Row 2:", StringComparison.Ordinal) && e.Contains("does not resolve"));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    [Theory]
    [InlineData("true", "true")]   // two correct
    [InlineData("false", "false")] // zero correct
    public async Task Import_WrongCorrectAnswerCount_RejectedBeforeSave_NothingPersisted(string option1Correct, string option2Correct)
    {
        // Arrange
        var csv = BuildCsv(new CsvRow
        {
            Stem = "Bad question?",
            Codes = "Code1",
            Section = "Rules",
            LanguageCode = "en",
            Option1Text = "Option A",
            Option1Correct = option1Correct,
            Option2Text = "Option B",
            Option2Correct = option2Correct
        });

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ImportQuestionsCommand("bank.csv", csv), CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.StartsWith("Row 1:", StringComparison.Ordinal));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    [Fact]
    public async Task RequestCsvTemplate_ReturnsNonEmptyCsvWithDocumentedHeaders()
    {
        // Arrange
        var handler = new CreateQuestionsImportTemplateCommandHandler();

        // Act
        var result = await handler.Handle(new CreateQuestionsImportTemplateCommand { Format = "csv" }, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data!);

        using var reader = new StreamReader(new MemoryStream(result.Data!));
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        Assert.True(csv.Read());
        csv.ReadHeader();
        Assert.Equal(QuestionImportCsvColumns.AllHeaders().ToArray(), csv.HeaderRecord);

        // The template's example row must itself be importable.
        Assert.True(csv.Read());
    }

    [Fact]
    public async Task RequestJsonTemplate_ReturnsNonEmptyValidJsonMatchingDocumentedShape()
    {
        // Arrange
        var handler = new CreateQuestionsImportTemplateCommandHandler();

        // Act
        var result = await handler.Handle(new CreateQuestionsImportTemplateCommand { Format = "json" }, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data!);

        using var document = JsonDocument.Parse(result.Data!);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        var row = document.RootElement.EnumerateArray().Single();
        Assert.True(row.TryGetProperty("stem", out _));
        Assert.True(row.TryGetProperty("codes", out var codes) && codes.ValueKind == JsonValueKind.Array);
        Assert.True(row.TryGetProperty("section", out _));
        Assert.True(row.TryGetProperty("languageCode", out _));
        Assert.True(row.TryGetProperty("signRef", out _));
        Assert.True(row.TryGetProperty("answerOptions", out var options) && options.ValueKind == JsonValueKind.Array);
        var firstOption = options.EnumerateArray().First();
        Assert.True(firstOption.TryGetProperty("text", out _));
        Assert.True(firstOption.TryGetProperty("isCorrect", out _));
    }
}
