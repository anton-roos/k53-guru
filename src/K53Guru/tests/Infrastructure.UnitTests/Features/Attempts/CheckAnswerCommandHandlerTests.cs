using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using K53Guru.Application.Common.ExceptionHandlers;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.Attempts.Commands.CheckAnswer;
using K53Guru.Application.Features.Attempts.Commands.Start;
using K53Guru.Application.Features.Attempts.Commands.Submit;
using K53Guru.Domain.Entities;
using K53Guru.Domain.Enums;
using K53Guru.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Features.Attempts;

/// <summary>
/// Covers spec-3-6-practice-and-test-modes.md's check-answer I/O &amp; Edge-Case Matrix rows
/// directly against the production CheckAnswerCommandHandler, mirroring
/// StartAttemptCommandHandlerTests.cs's/SubmitAttemptCommandHandlerTests.cs's SQLite in-memory
/// harness. Each test starts a real Attempt via the production StartAttemptCommandHandler first.
///
/// Matrix rows covered:
///   - Check answer, Practice mode -&gt; returns IsCorrect/CorrectAttemptAnswerOptionId/Explanation;
///     IsSelected updated
///   - Check answer, retry -&gt; second call's selection replaces the first; both calls return
///     correct feedback
///   - Check answer, Test mode -&gt; rejected; nothing revealed
///   - Check answer, already submitted -&gt; rejected
///
/// Plus regression coverage for Boundaries-documented behavior not itself a distinct matrix row:
/// the same wrong-learner/nonexistent-id ownership check as GetAttemptQuery/SubmitAttemptCommand,
/// and a 404-equivalent rejection for a foreign AttemptQuestion/AttemptAnswerOption.
/// </summary>
public class CheckAnswerCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IMapper _mapper;

    public CheckAnswerCommandHandlerTests()
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
            new MapperConfiguration(cfg => cfg.AddMaps(Assembly.GetAssembly(typeof(StartAttemptCommand))));
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

    private static Question NewQuestion(string stem, SectionType section, string? explanation = null) => new()
    {
        Stem = stem,
        Codes = LicenceCode.Code1,
        Section = section,
        LanguageCode = "en",
        Explanation = explanation,
        AnswerOptions = new List<AnswerOption>
        {
            new() { Text = "Correct answer", IsCorrect = true, Order = 0 },
            new() { Text = "Wrong answer", IsCorrect = false, Order = 1 }
        }
    };

    /// <summary>
    /// Seeds a published, single-code (Code1) Test with one question per section (Rules/Signs
    /// have no Explanation; VehicleControls carries one), PassMark 1 each.
    /// </summary>
    private async Task<int> SeedPublishedTestAsync()
    {
        await using var context = new ApplicationDbContext(_options);

        var questions = new List<Question>
        {
            NewQuestion("Rules Q0", SectionType.Rules),
            NewQuestion("Signs Q0", SectionType.Signs),
            NewQuestion("VehicleControls Q0", SectionType.VehicleControls, explanation: "Because the rule says so.")
        };

        var test = new Test
        {
            Name = "Sample Test",
            Codes = LicenceCode.Code1,
            Sections = TestSectionScope.Rules | TestSectionScope.Signs | TestSectionScope.VehicleControls,
            Status = TestStatus.Published,
            TestQuestions = questions.Select(q => new TestQuestion { Question = q }).ToList()
        };
        context.Tests.Add(test);

        context.TestConfigs.Add(new TestConfig
        {
            Code = LicenceCode.Code1,
            TimeLimitMinutes = 60,
            SectionRules = new List<SectionRule>
            {
                new() { Section = SectionType.Rules, QuestionCount = 1, PassMark = 1 },
                new() { Section = SectionType.Signs, QuestionCount = 1, PassMark = 1 },
                new() { Section = SectionType.VehicleControls, QuestionCount = 1, PassMark = 1 }
            }
        });

        await context.SaveChangesAsync();
        return test.Id;
    }

    private async Task<(int AttemptId, Guid LearnerProfileId)> StartAttemptAsync(int testId, AttemptMode mode)
    {
        var startHandler = new StartAttemptCommandHandler(CreateFactory(), _mapper);
        var learnerProfileId = Guid.NewGuid();
        var startResult = await startHandler.Handle(
            new StartAttemptCommand { LearnerProfileId = learnerProfileId, TestId = testId, Mode = mode },
            CancellationToken.None);
        Assert.True(startResult.Succeeded);
        return (startResult.Data!.Id, learnerProfileId);
    }

    private async Task<Attempt> ReadAttemptAsync(int attemptId)
    {
        await using var context = new ApplicationDbContext(_options);
        return await context.Attempts
            .Include(a => a.AttemptQuestions)
            .ThenInclude(q => q.AttemptAnswerOptions)
            .SingleAsync(a => a.Id == attemptId);
    }

    [Fact]
    public async Task CheckAnswer_PracticeMode_ReturnsCorrectnessAndExplanationAndUpdatesIsSelected()
    {
        // Arrange
        var testId = await SeedPublishedTestAsync();
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId, AttemptMode.Practice);
        var attempt = await ReadAttemptAsync(attemptId);
        var question = attempt.AttemptQuestions.Single(q => q.Section == SectionType.VehicleControls);
        var correctOption = question.AttemptAnswerOptions.Single(o => o.IsCorrect);

        var handler = new CheckAnswerCommandHandler(CreateFactory());

        // Act
        var result = await handler.Handle(
            new CheckAnswerCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                AttemptQuestionId = question.Id,
                SelectedAttemptAnswerOptionId = correctOption.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        var dto = result.Data!;
        Assert.True(dto.IsCorrect);
        Assert.Equal(correctOption.Id, dto.CorrectAttemptAnswerOptionId);
        Assert.Equal("Because the rule says so.", dto.Explanation);

        // IsSelected is updated on the snapshot - persisted, not just returned.
        await using var verifyContext = new ApplicationDbContext(_options);
        var savedOptions = await verifyContext.AttemptAnswerOptions
            .Where(o => o.AttemptQuestionId == question.Id)
            .ToListAsync();
        Assert.True(savedOptions.Single(o => o.Id == correctOption.Id).IsSelected);
        Assert.All(savedOptions.Where(o => o.Id != correctOption.Id), o => Assert.False(o.IsSelected));

        // No CodeResult/SectionResult is ever persisted by check-answer - that stays
        // SubmitAttemptCommand's job alone.
        Assert.Equal(0, await verifyContext.CodeResults.CountAsync(cr => cr.AttemptId == attemptId));
    }

    [Fact]
    public async Task CheckAnswer_WrongOption_ReturnsIsCorrectFalseButStillRevealsCorrectOptionAndExplanation()
    {
        // Arrange
        var testId = await SeedPublishedTestAsync();
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId, AttemptMode.Practice);
        var attempt = await ReadAttemptAsync(attemptId);
        var question = attempt.AttemptQuestions.Single(q => q.Section == SectionType.VehicleControls);
        var correctOption = question.AttemptAnswerOptions.Single(o => o.IsCorrect);
        var wrongOption = question.AttemptAnswerOptions.Single(o => !o.IsCorrect);

        var handler = new CheckAnswerCommandHandler(CreateFactory());

        // Act
        var result = await handler.Handle(
            new CheckAnswerCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                AttemptQuestionId = question.Id,
                SelectedAttemptAnswerOptionId = wrongOption.Id
            },
            CancellationToken.None);

        // Assert: IsCorrect reflects the SELECTED option, but the correct option/explanation are
        // still revealed regardless - Practice mode's whole point.
        Assert.True(result.Succeeded);
        Assert.False(result.Data!.IsCorrect);
        Assert.Equal(correctOption.Id, result.Data!.CorrectAttemptAnswerOptionId);
        Assert.Equal("Because the rule says so.", result.Data!.Explanation);
    }

    [Fact]
    public async Task CheckAnswer_Retry_SecondCallReplacesFirstSelection_BothReturnCorrectFeedback()
    {
        // Arrange: same question checked twice with different answers.
        var testId = await SeedPublishedTestAsync();
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId, AttemptMode.Practice);
        var attempt = await ReadAttemptAsync(attemptId);
        var question = attempt.AttemptQuestions.Single(q => q.Section == SectionType.VehicleControls);
        var correctOption = question.AttemptAnswerOptions.Single(o => o.IsCorrect);
        var wrongOption = question.AttemptAnswerOptions.Single(o => !o.IsCorrect);

        var handler = new CheckAnswerCommandHandler(CreateFactory());

        // Act: first call selects the wrong option.
        var first = await handler.Handle(
            new CheckAnswerCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                AttemptQuestionId = question.Id,
                SelectedAttemptAnswerOptionId = wrongOption.Id
            },
            CancellationToken.None);

        // Act: second call (the retry) selects the correct option.
        var second = await handler.Handle(
            new CheckAnswerCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                AttemptQuestionId = question.Id,
                SelectedAttemptAnswerOptionId = correctOption.Id
            },
            CancellationToken.None);

        // Assert: both calls succeed and return correct feedback; the second call's selection
        // REPLACES the first's, not adds to it.
        Assert.True(first.Succeeded);
        Assert.False(first.Data!.IsCorrect);
        Assert.True(second.Succeeded);
        Assert.True(second.Data!.IsCorrect);

        await using var verifyContext = new ApplicationDbContext(_options);
        var savedOptions = await verifyContext.AttemptAnswerOptions
            .Where(o => o.AttemptQuestionId == question.Id)
            .ToListAsync();
        Assert.True(savedOptions.Single(o => o.Id == correctOption.Id).IsSelected);
        Assert.False(savedOptions.Single(o => o.Id == wrongOption.Id).IsSelected);
    }

    [Fact]
    public async Task CheckAnswer_TestMode_RejectedNothingRevealed()
    {
        // Arrange: Test mode's confidentiality must never be bypassable through this endpoint.
        var testId = await SeedPublishedTestAsync();
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId, AttemptMode.Test);
        var attempt = await ReadAttemptAsync(attemptId);
        var question = attempt.AttemptQuestions.Single(q => q.Section == SectionType.VehicleControls);
        var someOption = question.AttemptAnswerOptions.First();

        var handler = new CheckAnswerCommandHandler(CreateFactory());

        // Act
        var result = await handler.Handle(
            new CheckAnswerCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                AttemptQuestionId = question.Id,
                SelectedAttemptAnswerOptionId = someOption.Id
            },
            CancellationToken.None);

        // Assert: rejected, nothing revealed, nothing mutated.
        Assert.False(result.Succeeded);
        Assert.Null(result.Data);
        Assert.Contains("Practice mode", result.ErrorMessage);

        await using var verifyContext = new ApplicationDbContext(_options);
        var savedOptions = await verifyContext.AttemptAnswerOptions
            .Where(o => o.AttemptQuestionId == question.Id)
            .ToListAsync();
        Assert.All(savedOptions, o => Assert.False(o.IsSelected));
    }

    [Fact]
    public async Task CheckAnswer_AlreadySubmitted_Rejected()
    {
        // Arrange: submit the (Practice-mode) attempt first, then try to check an answer against
        // the now-finished attempt.
        var testId = await SeedPublishedTestAsync();
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId, AttemptMode.Practice);
        var attempt = await ReadAttemptAsync(attemptId);

        var submitHandler = new SubmitAttemptCommandHandler(CreateFactory(), _mapper);
        var submitAnswers = attempt.AttemptQuestions.Select(q => new SubmitAttemptAnswer
        {
            AttemptQuestionId = q.Id,
            SelectedAttemptAnswerOptionId = q.AttemptAnswerOptions.Single(o => o.IsCorrect).Id
        }).ToList();
        var submitResult = await submitHandler.Handle(
            new SubmitAttemptCommand { AttemptId = attemptId, LearnerProfileId = learnerProfileId, Answers = submitAnswers },
            CancellationToken.None);
        Assert.True(submitResult.Succeeded);

        var question = attempt.AttemptQuestions.Single(q => q.Section == SectionType.VehicleControls);
        var handler = new CheckAnswerCommandHandler(CreateFactory());

        // Act
        var result = await handler.Handle(
            new CheckAnswerCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                AttemptQuestionId = question.Id,
                SelectedAttemptAnswerOptionId = question.AttemptAnswerOptions.First().Id
            },
            CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("already", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAnswer_WrongLearner_RejectedWithNotFoundException_SameAsResumeBehavior()
    {
        // Arrange
        var testId = await SeedPublishedTestAsync();
        var (attemptId, _) = await StartAttemptAsync(testId, AttemptMode.Practice);

        var handler = new CheckAnswerCommandHandler(CreateFactory());

        // Act & Assert: same ownership check as GetAttemptQuery/SubmitAttemptCommand - a
        // mismatched LearnerProfileId is rejected identically to a nonexistent AttemptId
        // (NotFoundException), never leaking the attempt's existence.
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new CheckAnswerCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = Guid.NewGuid(),
                AttemptQuestionId = 1,
                SelectedAttemptAnswerOptionId = 1
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task CheckAnswer_AttemptNotFound_RejectedWithNotFoundException()
    {
        // Arrange
        var handler = new CheckAnswerCommandHandler(CreateFactory());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new CheckAnswerCommand
            {
                AttemptId = int.MaxValue,
                LearnerProfileId = Guid.NewGuid(),
                AttemptQuestionId = 1,
                SelectedAttemptAnswerOptionId = 1
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task CheckAnswer_ForeignAttemptQuestionId_RejectedAsNotFound()
    {
        // Arrange: a real AttemptQuestion, but from a SECOND, unrelated attempt.
        var testId = await SeedPublishedTestAsync();
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId, AttemptMode.Practice);
        var (otherAttemptId, _) = await StartAttemptAsync(testId, AttemptMode.Practice);
        var otherAttempt = await ReadAttemptAsync(otherAttemptId);
        var foreignQuestion = otherAttempt.AttemptQuestions.First();

        var handler = new CheckAnswerCommandHandler(CreateFactory());

        // Act
        var result = await handler.Handle(
            new CheckAnswerCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                AttemptQuestionId = foreignQuestion.Id,
                SelectedAttemptAnswerOptionId = foreignQuestion.AttemptAnswerOptions.First().Id
            },
            CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAnswer_ForeignAttemptAnswerOptionId_RejectedAsNotFound()
    {
        // Arrange: a real AttemptQuestion from THIS attempt, but an option id belonging to a
        // DIFFERENT question entirely.
        var testId = await SeedPublishedTestAsync();
        var (attemptId, learnerProfileId) = await StartAttemptAsync(testId, AttemptMode.Practice);
        var attempt = await ReadAttemptAsync(attemptId);
        var thisQuestion = attempt.AttemptQuestions.Single(q => q.Section == SectionType.Rules);
        var foreignOptionId = attempt.AttemptQuestions.Single(q => q.Section == SectionType.Signs)
            .AttemptAnswerOptions.First().Id;

        var handler = new CheckAnswerCommandHandler(CreateFactory());

        // Act
        var result = await handler.Handle(
            new CheckAnswerCommand
            {
                AttemptId = attemptId,
                LearnerProfileId = learnerProfileId,
                AttemptQuestionId = thisQuestion.Id,
                SelectedAttemptAnswerOptionId = foreignOptionId
            },
            CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
