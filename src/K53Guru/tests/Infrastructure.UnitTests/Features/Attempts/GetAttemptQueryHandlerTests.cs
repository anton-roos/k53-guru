using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using K53Guru.Application.Common.ExceptionHandlers;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.Attempts.Commands.Start;
using K53Guru.Application.Features.Attempts.Queries.GetById;
using K53Guru.Domain.Entities;
using K53Guru.Domain.Enums;
using K53Guru.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Features.Attempts;

/// <summary>
/// Covers spec-3-3-start-single-code-attempt.md's resume-side I/O &amp; Edge-Case Matrix rows
/// directly against the production GetAttemptQueryHandler, mirroring
/// StartAttemptCommandHandlerTests.cs's SQLite in-memory harness.
///
/// Matrix rows covered:
///   - Resume, content edited since start -&gt; the original (pre-edit) snapshotted Stem is
///     returned unchanged
///   - Resume, called twice -&gt; both calls return identical DisplayOrders and content
///   - Resume, wrong learner -&gt; rejected, same as not-found (NotFoundException, per this
///     codebase's GetById convention - see RoadSignsQueryHandlerTests.cs)
///   - Resume, nonexistent AttemptId -&gt; rejected the same way
/// </summary>
public class GetAttemptQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IMapper _mapper;

    public GetAttemptQueryHandlerTests()
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
            new MapperConfiguration(cfg => cfg.AddMaps(Assembly.GetAssembly(typeof(GetAttemptQuery))));
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

    private static Question NewQuestion(string stem, SectionType section) => new()
    {
        Stem = stem,
        Codes = LicenceCode.Code1,
        Section = section,
        LanguageCode = "en",
        AnswerOptions = new List<AnswerOption>
        {
            new() { Text = "Correct answer", IsCorrect = true, Order = 0 },
            new() { Text = "Wrong answer", IsCorrect = false, Order = 1 }
        }
    };

    /// <summary>
    /// Seeds a published, single-code (Code1) Test with 1 question per section and a matching
    /// TestConfig requiring exactly 1 per section, then starts (and persists) an Attempt for it
    /// via the real StartAttemptCommandHandler - the same composition path GetAttemptQuery must
    /// then faithfully resume.
    /// </summary>
    private async Task<(int AttemptId, Guid LearnerProfileId, int QuestionId)> SeedStartedAttemptAsync()
    {
        int questionId;
        await using (var context = new ApplicationDbContext(_options))
        {
            var rulesQuestion = NewQuestion("Original stem", SectionType.Rules);
            var test = new Test
            {
                Name = "Sample Test",
                Codes = LicenceCode.Code1,
                Sections = TestSectionScope.Rules | TestSectionScope.Signs | TestSectionScope.VehicleControls,
                Status = TestStatus.Published,
                TestQuestions = new List<TestQuestion>
                {
                    new() { Question = rulesQuestion },
                    new() { Question = NewQuestion("Signs Q", SectionType.Signs) },
                    new() { Question = NewQuestion("Controls Q", SectionType.VehicleControls) }
                }
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

            var startHandler = new StartAttemptCommandHandler(CreateFactory(), _mapper);
            var learnerProfileId = Guid.NewGuid();
            var startResult = await startHandler.Handle(
                new StartAttemptCommand { LearnerProfileId = learnerProfileId, TestId = test.Id },
                CancellationToken.None);
            Assert.True(startResult.Succeeded);

            questionId = rulesQuestion.Id;
            return (startResult.Data!.Id, learnerProfileId, questionId);
        }
    }

    [Fact]
    public async Task Resume_ContentEditedSinceStart_ReturnsOriginalSnapshottedStemUnchanged()
    {
        // Arrange
        var (attemptId, learnerProfileId, questionId) = await SeedStartedAttemptAsync();

        await using (var editContext = new ApplicationDbContext(_options))
        {
            var question = await editContext.Questions.SingleAsync(q => q.Id == questionId);
            question.Stem = "Edited stem - should never surface on resume";
            await editContext.SaveChangesAsync();
        }

        var handler = new GetAttemptQueryHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(
            new GetAttemptQuery { AttemptId = attemptId, LearnerProfileId = learnerProfileId },
            CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        var rulesQuestion = result.Data!.AttemptQuestions.Single(q => q.Section == SectionType.Rules);
        Assert.Equal("Original stem", rulesQuestion.Stem);
    }

    [Fact]
    public async Task Resume_CalledTwice_ReturnsIdenticalDisplayOrdersAndContentBothTimes()
    {
        // Arrange
        var (attemptId, learnerProfileId, _) = await SeedStartedAttemptAsync();
        var handler = new GetAttemptQueryHandler(CreateFactory(), _mapper);
        var query = new GetAttemptQuery { AttemptId = attemptId, LearnerProfileId = learnerProfileId };

        // Act
        var first = await handler.Handle(query, CancellationToken.None);
        var second = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);

        var firstOrder = first.Data!.AttemptQuestions.Select(q => (q.DisplayOrder, q.Stem)).ToList();
        var secondOrder = second.Data!.AttemptQuestions.Select(q => (q.DisplayOrder, q.Stem)).ToList();

        Assert.Equal(firstOrder, secondOrder);
    }

    [Fact]
    public async Task Resume_WrongLearner_RejectedSameAsNotFound()
    {
        // Arrange
        var (attemptId, _, _) = await SeedStartedAttemptAsync();
        var handler = new GetAttemptQueryHandler(CreateFactory(), _mapper);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetAttemptQuery { AttemptId = attemptId, LearnerProfileId = Guid.NewGuid() },
            CancellationToken.None));
    }

    [Fact]
    public async Task Resume_NonexistentAttemptId_RejectedSameAsWrongLearner()
    {
        // Arrange
        var handler = new GetAttemptQueryHandler(CreateFactory(), _mapper);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetAttemptQuery { AttemptId = int.MaxValue, LearnerProfileId = Guid.NewGuid() },
            CancellationToken.None));
    }

    [Fact]
    public async Task Resume_WrongLearnerMessage_IsIdenticalToNonexistentIdMessage_ForTheSameAttemptId()
    {
        // Arrange: seed a real, persisted Attempt to learn a concrete AttemptId.
        var (attemptId, _, _) = await SeedStartedAttemptAsync();
        var handler = new GetAttemptQueryHandler(CreateFactory(), _mapper);

        // Act: wrong learner - the AttemptId exists, but belongs to a different LearnerProfileId.
        var wrongLearnerException = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetAttemptQuery { AttemptId = attemptId, LearnerProfileId = Guid.NewGuid() },
            CancellationToken.None));

        // Act: nonexistent - the exact same AttemptId value, resolved against a second, completely
        // empty database where no Attempt with that id was ever created. Using the same numeric id
        // in both cases isolates the comparison to "why" the attempt can't be resolved, so the
        // resulting messages are directly comparable.
        using var emptyConnection = new SqliteConnection("DataSource=:memory:");
        emptyConnection.Open();
        var emptyOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(emptyConnection).Options;
        using (var schemaContext = new ApplicationDbContext(emptyOptions))
        {
            schemaContext.Database.EnsureCreated();
        }

        var emptyFactoryMock = new Mock<IApplicationDbContextFactory>();
        emptyFactoryMock
            .Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (IApplicationDbContext)new ApplicationDbContext(emptyOptions));
        var emptyHandler = new GetAttemptQueryHandler(emptyFactoryMock.Object, _mapper);

        var nonexistentException = await Assert.ThrowsAsync<NotFoundException>(() => emptyHandler.Handle(
            new GetAttemptQuery { AttemptId = attemptId, LearnerProfileId = Guid.NewGuid() },
            CancellationToken.None));

        // Assert: not just the same exception type, but byte-for-byte the same message. The spec
        // requires the wrong-learner case to be indistinguishable from a genuinely nonexistent id
        // ("never leaking another learner's attempt's existence") - a future change that gave the
        // wrong-learner case a different message (e.g. "not authorized") while keeping "not found"
        // for the missing-id case would leak existence information and must fail this test.
        Assert.Equal(nonexistentException.Message, wrongLearnerException.Message);
    }
}
