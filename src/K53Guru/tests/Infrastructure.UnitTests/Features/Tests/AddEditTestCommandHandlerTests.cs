using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation.Results;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.Questions.Queries.GetAll;
using K53Guru.Application.Features.Tests;
using K53Guru.Application.Features.Tests.Commands.AddEdit;
using K53Guru.Domain.Entities;
using K53Guru.Domain.Enums;
using K53Guru.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Features.Tests;

/// <summary>
/// Covers spec-2-2-organise-questions-into-test.md I/O &amp; Edge-Case Matrix rows directly
/// against the production validator + handler, bypassing the DI+Respawn integration harness used
/// elsewhere in this solution (mirrors AddEditQuestionCommandHandlerTests.cs's rationale: no live
/// MSSQL/PostgreSQL instance is reachable in this sandbox).
///
/// Uses a shared-connection SQLite in-memory ApplicationDbContext - schema derived from the EF
/// model via EnsureCreated() - and invokes the real AddEditTestCommandValidator then
/// AddEditTestCommandHandler production classes in sequence, the same way MediatR's
/// ValidationPreProcessor -> handler pipeline would (see Application.DependencyInjection).
///
/// Matrix rows covered:
///   - Create, valid
///   - Create, missing name
///   - Create, no codes
///   - Create, no sections
///   - Create, zero questions
///   - Edit, reconciles questions
///   - View, grouped counts
///
/// Also covers two review-flagged gaps beyond the base matrix rows:
///   - Codes/Sections carrying a bit outside the known flag values (not just the all-zero case)
///   - A submitted QuestionId that does not reference an existing Question row
/// </summary>
public class AddEditTestCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IMapper _mapper;

    public AddEditTestCommandHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var schemaContext = new ApplicationDbContext(_options))
        {
            schemaContext.Database.EnsureCreated();
        }

        var mapperConfiguration =
            new MapperConfiguration(cfg => cfg.AddMaps(Assembly.GetAssembly(typeof(AddEditTestCommand))));
        _mapper = mapperConfiguration.CreateMapper();
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

    private static Question NewQuestion(string stem, LicenceCode codes, SectionType section) => new()
    {
        Stem = stem,
        Codes = codes,
        Section = section,
        LanguageCode = "en",
        AnswerOptions = new List<AnswerOption>
        {
            new() { Text = "Correct answer", IsCorrect = true, Order = 0 },
            new() { Text = "Wrong answer", IsCorrect = false, Order = 1 }
        }
    };

    private async Task<List<int>> SeedQuestionsAsync(params Question[] questions)
    {
        await using var context = new ApplicationDbContext(_options);
        context.Questions.AddRange(questions);
        await context.SaveChangesAsync();
        return questions.Select(q => q.Id).ToList();
    }

    private static AddEditTestCommand ValidCommand(List<int> questionIds) => new()
    {
        Name = "Sample Test",
        Codes = LicenceCode.Code1,
        Sections = TestSectionScope.Rules,
        QuestionIds = questionIds
    };

    private async Task<List<ValidationFailure>> ValidateAsync(AddEditTestCommand command)
    {
        var validator = new AddEditTestCommandValidator(CreateFactory());
        var result = await validator.ValidateAsync(command);
        return result.Errors;
    }

    [Fact]
    public async Task Create_Valid_SavesTestAsDraftWithAssociatedQuestions()
    {
        // Arrange
        var questionIds = await SeedQuestionsAsync(
            NewQuestion("Q1", LicenceCode.Code1, SectionType.Rules),
            NewQuestion("Q2", LicenceCode.Code1, SectionType.Rules));

        var command = ValidCommand(questionIds);

        // Act
        var failures = await ValidateAsync(command);
        Assert.Empty(failures);

        var handler = new AddEditTestCommandHandler(CreateFactory(), _mapper);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.Data > 0);

        await using var verifyContext = new ApplicationDbContext(_options);
        var saved = await verifyContext.Tests.Include(t => t.TestQuestions).SingleAsync();
        Assert.Equal("Sample Test", saved.Name);
        Assert.Equal(TestStatus.Draft, saved.Status);
        Assert.Equal(2, saved.TestQuestions.Count);
        Assert.Equal(questionIds.ToHashSet(), saved.TestQuestions.Select(tq => tq.QuestionId).ToHashSet());
    }

    [Fact]
    public async Task Create_MissingName_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange
        var questionIds = await SeedQuestionsAsync(NewQuestion("Q1", LicenceCode.Code1, SectionType.Rules));
        var command = ValidCommand(questionIds);
        command.Name = string.Empty;

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditTestCommand.Name));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Tests.CountAsync());
    }

    [Fact]
    public async Task Create_NoCodes_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange
        var questionIds = await SeedQuestionsAsync(NewQuestion("Q1", LicenceCode.Code1, SectionType.Rules));
        var command = ValidCommand(questionIds);
        command.Codes = LicenceCode.None;

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditTestCommand.Codes));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Tests.CountAsync());
    }

    [Fact]
    public async Task Create_CodesHasBitOutsideKnownValues_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange: a stray bit beyond Code1|Code2|Code3 (e.g. from a malformed direct API call -
        // unreachable through the UI's checkbox-style selector, but not through the command
        // itself).
        var questionIds = await SeedQuestionsAsync(NewQuestion("Q1", LicenceCode.Code1, SectionType.Rules));
        var command = ValidCommand(questionIds);
        command.Codes = (LicenceCode)(1 << 5);

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditTestCommand.Codes));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Tests.CountAsync());
    }

    [Fact]
    public async Task Create_NoSections_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange
        var questionIds = await SeedQuestionsAsync(NewQuestion("Q1", LicenceCode.Code1, SectionType.Rules));
        var command = ValidCommand(questionIds);
        command.Sections = TestSectionScope.None;

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditTestCommand.Sections));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Tests.CountAsync());
    }

    [Fact]
    public async Task Create_SectionsHasBitOutsideKnownValues_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange: a stray bit beyond Rules|Signs|VehicleControls.
        var questionIds = await SeedQuestionsAsync(NewQuestion("Q1", LicenceCode.Code1, SectionType.Rules));
        var command = ValidCommand(questionIds);
        command.Sections = (TestSectionScope)(1 << 5);

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditTestCommand.Sections));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Tests.CountAsync());
    }

    [Fact]
    public async Task Create_ZeroQuestions_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange
        var command = ValidCommand(new List<int>());

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditTestCommand.QuestionIds));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Tests.CountAsync());
    }

    [Fact]
    public async Task Create_QuestionIdDoesNotExist_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange: one real question plus a stale/nonexistent id (e.g. from a race with another
        // admin, or any non-UI caller).
        var questionIds = await SeedQuestionsAsync(NewQuestion("Q1", LicenceCode.Code1, SectionType.Rules));
        var staleId = questionIds.Max() + 1000;
        var command = ValidCommand(new List<int>(questionIds) { staleId });

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditTestCommand.QuestionIds));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Tests.CountAsync());
    }

    [Fact]
    public async Task Edit_ReconcilesQuestions_JoinTableReflectsFinalMembershipExactly()
    {
        // Arrange: create a test with two questions.
        var questionIds = await SeedQuestionsAsync(
            NewQuestion("Keep me", LicenceCode.Code1, SectionType.Rules),
            NewQuestion("Remove me", LicenceCode.Code1, SectionType.Rules));
        var keepId = questionIds[0];
        var removeId = questionIds[1];

        var createCommand = ValidCommand(new List<int> { keepId, removeId });
        Assert.Empty(await ValidateAsync(createCommand));

        var handler = new AddEditTestCommandHandler(CreateFactory(), _mapper);
        var createResult = await handler.Handle(createCommand, CancellationToken.None);
        Assert.True(createResult.Succeeded);

        int testId;
        await using (var context = new ApplicationDbContext(_options))
        {
            testId = await context.Tests.Select(t => t.Id).SingleAsync();
        }

        // Act: edit - add a brand-new question, drop "Remove me", keep "Keep me".
        var addedIds = await SeedQuestionsAsync(NewQuestion("Added", LicenceCode.Code1, SectionType.Rules));
        var addedId = addedIds[0];

        var editCommand = ValidCommand(new List<int> { keepId, addedId });
        editCommand.Id = testId;
        Assert.Empty(await ValidateAsync(editCommand));

        var editResult = await handler.Handle(editCommand, CancellationToken.None);

        // Assert
        Assert.True(editResult.Succeeded);

        await using var verifyContext = new ApplicationDbContext(_options);
        var updated = await verifyContext.Tests.Include(t => t.TestQuestions)
            .SingleAsync(t => t.Id == testId);

        Assert.Equal(2, updated.TestQuestions.Count);
        Assert.Equal(new HashSet<int> { keepId, addedId },
            updated.TestQuestions.Select(tq => tq.QuestionId).ToHashSet());
        Assert.DoesNotContain(updated.TestQuestions, tq => tq.QuestionId == removeId);

        // No duplicate join rows for any question id.
        Assert.Equal(updated.TestQuestions.Count, updated.TestQuestions.Select(tq => tq.QuestionId).Distinct().Count());

        // Editing a Test must never change its Status.
        Assert.Equal(TestStatus.Draft, updated.Status);
    }

    [Fact]
    public async Task View_GroupedCounts_PerSectionAndPerCodeCountsAreCorrect()
    {
        // Arrange: questions spanning Rules/Signs sections and Code1/Code2 codes.
        var questionIds = await SeedQuestionsAsync(
            NewQuestion("Rules Code1 A", LicenceCode.Code1, SectionType.Rules),
            NewQuestion("Rules Code1 B", LicenceCode.Code1, SectionType.Rules),
            NewQuestion("Rules Code2", LicenceCode.Code2, SectionType.Rules),
            NewQuestion("Signs Code1", LicenceCode.Code1, SectionType.Signs),
            NewQuestion("Signs Code1+Code2", LicenceCode.Code1 | LicenceCode.Code2, SectionType.Signs));

        var command = new AddEditTestCommand
        {
            Name = "Grouped Test",
            Codes = LicenceCode.Code1 | LicenceCode.Code2,
            Sections = TestSectionScope.Rules | TestSectionScope.Signs,
            QuestionIds = questionIds
        };
        Assert.Empty(await ValidateAsync(command));

        var handler = new AddEditTestCommandHandler(CreateFactory(), _mapper);
        var result = await handler.Handle(command, CancellationToken.None);
        Assert.True(result.Succeeded);

        // Act: fetch via the real GetAllQuestionsQueryHandler (the same source TestFormDialog.razor
        // uses to populate its question list) and run the exact shipped TestQuestionGrouping
        // helper - not a parallel re-implementation - so a regression in either the mapping or the
        // grouping/counting logic actually fails this test.
        var allQuestionsHandler = new GetAllQuestionsQueryHandler(CreateFactory(), _mapper);
        var allQuestions = (await allQuestionsHandler.Handle(new GetAllQuestionsQuery(), CancellationToken.None)).ToList();
        var associatedQuestions = allQuestions.Where(q => questionIds.Contains(q.Id));

        var grouped = TestQuestionGrouping.GroupBySectionWithCodeCounts(associatedQuestions);

        // Assert: sections are ordered Rules -> Signs -> VehicleControls, and per-section counts
        // are correct.
        Assert.Equal(new[] { SectionType.Rules, SectionType.Signs }, grouped.Select(g => g.Section));
        var rules = grouped.Single(g => g.Section == SectionType.Rules);
        var signs = grouped.Single(g => g.Section == SectionType.Signs);
        Assert.Equal(3, rules.Count);
        Assert.Equal(2, signs.Count);

        // Assert: per-code counts within the Rules section.
        Assert.Equal(2, rules.CodeCounts.Single(c => c.Code == nameof(LicenceCode.Code1)).Count);
        Assert.Equal(1, rules.CodeCounts.Single(c => c.Code == nameof(LicenceCode.Code2)).Count);

        // Assert: per-code counts within the Signs section - the dual-code question counts once
        // toward each of its codes.
        Assert.Equal(2, signs.CodeCounts.Single(c => c.Code == nameof(LicenceCode.Code1)).Count);
        Assert.Equal(1, signs.CodeCounts.Single(c => c.Code == nameof(LicenceCode.Code2)).Count);
    }
}
