using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation.Results;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.Questions.Commands.AddEdit;
using K53Guru.Domain.Entities;
using K53Guru.Domain.Enums;
using K53Guru.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Features.Questions;

/// <summary>
/// Covers spec-2-1-author-edit-question.md I/O &amp; Edge-Case Matrix rows directly against the
/// production validator + handler, bypassing the DI+Respawn integration harness used elsewhere
/// in this solution (mirrors RoadSignsQueryHandlerTests.cs's rationale: no live MSSQL/PostgreSQL
/// instance is reachable in this sandbox).
///
/// Uses a shared-connection SQLite in-memory ApplicationDbContext - schema derived from the EF
/// model via EnsureCreated() - and invokes the real AddEditQuestionCommandValidator then
/// AddEditQuestionCommandHandler production classes in sequence, the same way MediatR's
/// ValidationPreProcessor -> handler pipeline would (see Application.DependencyInjection).
///
/// Matrix rows covered:
///   - Create, valid
///   - Create, missing stem
///   - Create, no codes
///   - Create, zero/multiple correct
///   - Create, unresolved sign_ref
///   - Edit, reconciles options
///   - Edit, validation fails
/// </summary>
public class AddEditQuestionCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IMapper _mapper;

    public AddEditQuestionCommandHandlerTests()
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
            new MapperConfiguration(cfg => cfg.AddMaps(Assembly.GetAssembly(typeof(AddEditQuestionCommand))));
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

    private static AddEditQuestionCommand ValidCommand(string? signRef = null) => new()
    {
        Stem = "What does this sign mean?",
        Codes = LicenceCode.Code1,
        Section = SectionType.Signs,
        LanguageCode = "en",
        SignRef = signRef,
        AnswerOptions = new List<AnswerOptionModel>
        {
            new() { Text = "Correct answer", IsCorrect = true },
            new() { Text = "Wrong answer", IsCorrect = false }
        }
    };

    private async Task<List<ValidationFailure>> ValidateAsync(AddEditQuestionCommand command)
    {
        var validator = new AddEditQuestionCommandValidator(CreateFactory());
        var result = await validator.ValidateAsync(command);
        return result.Errors;
    }

    [Fact]
    public async Task Create_Valid_SavesQuestionAndAnswerOptions()
    {
        // Arrange
        await using (var seedContext = new ApplicationDbContext(_options))
        {
            seedContext.RoadSigns.Add(new RoadSign { LegislationCode = "R1", Description = "Stop" });
            await seedContext.SaveChangesAsync();
        }

        var command = ValidCommand(signRef: "R1");

        // Act
        var failures = await ValidateAsync(command);
        Assert.Empty(failures);

        var handler = new AddEditQuestionCommandHandler(CreateFactory(), _mapper);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.Data > 0);

        await using var verifyContext = new ApplicationDbContext(_options);
        var saved = await verifyContext.Questions.Include(q => q.AnswerOptions).SingleAsync();
        Assert.Equal("What does this sign mean?", saved.Stem);
        Assert.Equal("R1", saved.SignRef);
        Assert.Equal(2, saved.AnswerOptions.Count);
        Assert.Single(saved.AnswerOptions, a => a.IsCorrect);
    }

    [Fact]
    public async Task Create_MissingStem_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange
        var command = ValidCommand();
        command.Stem = string.Empty;

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditQuestionCommand.Stem));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    [Fact]
    public async Task Create_NoCodes_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange
        var command = ValidCommand();
        command.Codes = LicenceCode.None;

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditQuestionCommand.Codes));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    [Fact]
    public async Task Create_CodesHasBitOutsideKnownValues_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange: a stray bit beyond Code1|Code2|Code3 (e.g. from a malformed direct API/import
        // call - unreachable through the UI's checkbox-style selector, but not through the
        // command itself).
        var command = ValidCommand();
        command.Codes = (LicenceCode)(1 << 5);

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditQuestionCommand.Codes));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Create_ZeroOrMultipleCorrectAnswers_RejectedBeforeSave_NothingPersisted(int correctCount)
    {
        // Arrange
        var command = ValidCommand();
        command.AnswerOptions = new List<AnswerOptionModel>
        {
            new() { Text = "Option A", IsCorrect = correctCount >= 1 },
            new() { Text = "Option B", IsCorrect = correctCount >= 2 }
        };

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditQuestionCommand.AnswerOptions));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_AnswerOptionTextBlank_RejectedBeforeSave_NothingPersisted(string blankText)
    {
        // Arrange: nothing server-side rejected a blank option text before this fix - only the
        // MudTextField's client-side Required guarded it, so a caller bypassing the UI (a future
        // API endpoint, Story 2.4's CSV/JSON import reusing this command) could persist one.
        var command = ValidCommand();
        command.AnswerOptions = new List<AnswerOptionModel>
        {
            new() { Text = blankText, IsCorrect = true },
            new() { Text = "Wrong answer", IsCorrect = false }
        };

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName.EndsWith("Text", StringComparison.Ordinal));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    [Fact]
    public async Task Create_UnresolvedSignRef_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange: no RoadSign seeded, so "NOPE" cannot resolve.
        var command = ValidCommand(signRef: "NOPE");

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditQuestionCommand.SignRef));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Questions.CountAsync());
    }

    [Fact]
    public async Task Edit_ReconcilesAnswerOptions_AddsRemovesAndUpdatesWithoutOrphans()
    {
        // Arrange: create a question with 3 options.
        var createCommand = ValidCommand();
        createCommand.AnswerOptions = new List<AnswerOptionModel>
        {
            new() { Text = "Keep me (edited)", IsCorrect = false },
            new() { Text = "Remove me", IsCorrect = true },
            new() { Text = "Also keep me", IsCorrect = false }
        };
        Assert.Empty(await ValidateAsync(createCommand));

        var handler = new AddEditQuestionCommandHandler(CreateFactory(), _mapper);
        var createResult = await handler.Handle(createCommand, CancellationToken.None);
        Assert.True(createResult.Succeeded);

        int questionId;
        int keepId, removeId, keepId2;
        await using (var context = new ApplicationDbContext(_options))
        {
            var saved = await context.Questions.Include(q => q.AnswerOptions).SingleAsync();
            questionId = saved.Id;
            keepId = saved.AnswerOptions.Single(a => a.Text == "Keep me (edited)").Id;
            removeId = saved.AnswerOptions.Single(a => a.Text == "Remove me").Id;
            keepId2 = saved.AnswerOptions.Single(a => a.Text == "Also keep me").Id;
        }

        // Act: edit - update two existing options, remove one, add one, and move "correct" to
        // the brand-new option.
        var editCommand = ValidCommand();
        editCommand.Id = questionId;
        editCommand.AnswerOptions = new List<AnswerOptionModel>
        {
            new() { Id = keepId, Text = "Keep me (edited)", IsCorrect = false },
            new() { Id = keepId2, Text = "Also keep me", IsCorrect = false },
            new() { Text = "Brand new option", IsCorrect = true }
        };
        Assert.Empty(await ValidateAsync(editCommand));

        var editResult = await handler.Handle(editCommand, CancellationToken.None);

        // Assert
        Assert.True(editResult.Succeeded);

        await using var verifyContext = new ApplicationDbContext(_options);
        var updated = await verifyContext.Questions.Include(q => q.AnswerOptions)
            .SingleAsync(q => q.Id == questionId);
        Assert.Equal(3, updated.AnswerOptions.Count);
        Assert.DoesNotContain(updated.AnswerOptions, a => a.Id == removeId);
        Assert.Contains(updated.AnswerOptions, a => a.Id == keepId && a.Text == "Keep me (edited)");
        Assert.Contains(updated.AnswerOptions, a => a.Id == keepId2 && a.Text == "Also keep me");
        Assert.Single(updated.AnswerOptions, a => a.IsCorrect);
        Assert.Equal("Brand new option", updated.AnswerOptions.Single(a => a.IsCorrect).Text);

        // Orphan check: the removed AnswerOption row must actually be gone, not just unlinked.
        Assert.False(await verifyContext.AnswerOptions.AnyAsync(a => a.Id == removeId));

        // Order is derived strictly from the submitted array position, never client input.
        Assert.Equal(0, updated.AnswerOptions.Single(a => a.Id == keepId).Order);
        Assert.Equal(1, updated.AnswerOptions.Single(a => a.Id == keepId2).Order);
        Assert.Equal(2, updated.AnswerOptions.Single(a => a.IsCorrect).Order);
    }

    [Fact]
    public async Task Edit_ValidationFails_OriginalRowAndOptionsUnchanged()
    {
        // Arrange: create a valid question.
        var createCommand = ValidCommand();
        var handler = new AddEditQuestionCommandHandler(CreateFactory(), _mapper);
        Assert.Empty(await ValidateAsync(createCommand));
        var createResult = await handler.Handle(createCommand, CancellationToken.None);
        Assert.True(createResult.Succeeded);

        int questionId;
        await using (var context = new ApplicationDbContext(_options))
        {
            questionId = await context.Questions.Select(q => q.Id).SingleAsync();
        }

        // Act: edit introduces zero correct answers.
        var editCommand = ValidCommand();
        editCommand.Id = questionId;
        editCommand.AnswerOptions = new List<AnswerOptionModel>
        {
            new() { Text = "Wrong A", IsCorrect = false },
            new() { Text = "Wrong B", IsCorrect = false }
        };

        var failures = await ValidateAsync(editCommand);

        // Assert: validation rejects before the handler (and thus SaveChangesAsync) ever runs.
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditQuestionCommand.AnswerOptions));

        await using var verifyContext = new ApplicationDbContext(_options);
        var unchanged = await verifyContext.Questions.Include(q => q.AnswerOptions)
            .SingleAsync(q => q.Id == questionId);
        Assert.Equal(2, unchanged.AnswerOptions.Count);
        Assert.Single(unchanged.AnswerOptions, a => a.IsCorrect);
        Assert.Equal("Correct answer", unchanged.AnswerOptions.Single(a => a.IsCorrect).Text);
    }
}
