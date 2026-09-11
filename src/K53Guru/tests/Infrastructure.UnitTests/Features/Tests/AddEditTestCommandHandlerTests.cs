using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation.Results;
using K53Guru.Application.Common.Interfaces;
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
/// Covers spec-2-2-organise-questions-into-test.md's surviving I/O &amp; Edge-Case Matrix rows
/// directly against the production validator + handler, bypassing the DI+Respawn integration
/// harness used elsewhere in this solution (mirrors AddEditQuestionCommandHandlerTests.cs's
/// rationale: no live MSSQL/PostgreSQL instance is reachable in this sandbox).
///
/// Uses a shared-connection SQLite in-memory ApplicationDbContext - schema derived from the EF
/// model via EnsureCreated() - and invokes the real AddEditTestCommandValidator then
/// AddEditTestCommandHandler production classes in sequence, the same way MediatR's
/// ValidationPreProcessor -> handler pipeline would (see Application.DependencyInjection).
///
/// A Test no longer curates an explicit question pool (QuestionIds/TestQuestion were removed -
/// attempt composition draws straight from the question bank, filtered by Codes/Sections, at
/// StartAttemptCommand time - see StartAttemptCommandHandlerTests.cs), so this command only ever
/// persists a Test's scope (Name/Codes/Sections/Status).
///
/// Matrix rows covered:
///   - Create, valid
///   - Create, missing name
///   - Create, no codes
///   - Create, no sections
///   - Edit, updates scalar fields without touching Status
///
/// Also covers a review-flagged gap beyond the base matrix rows:
///   - Codes/Sections carrying a bit outside the known flag values (not just the all-zero case)
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

    private static AddEditTestCommand ValidCommand() => new()
    {
        Name = "Sample Test",
        Codes = LicenceCode.Code1,
        Sections = TestSectionScope.Rules
    };

    private async Task<List<ValidationFailure>> ValidateAsync(AddEditTestCommand command)
    {
        var validator = new AddEditTestCommandValidator();
        var result = await validator.ValidateAsync(command);
        return result.Errors;
    }

    [Fact]
    public async Task Create_Valid_SavesTestAsDraft()
    {
        // Arrange
        var command = ValidCommand();

        // Act
        var failures = await ValidateAsync(command);
        Assert.Empty(failures);

        var handler = new AddEditTestCommandHandler(CreateFactory(), _mapper);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.Data > 0);

        await using var verifyContext = new ApplicationDbContext(_options);
        var saved = await verifyContext.Tests.SingleAsync();
        Assert.Equal("Sample Test", saved.Name);
        Assert.Equal(LicenceCode.Code1, saved.Codes);
        Assert.Equal(TestSectionScope.Rules, saved.Sections);
        Assert.Equal(TestStatus.Draft, saved.Status);
    }

    [Fact]
    public async Task Create_MissingName_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange
        var command = ValidCommand();
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
        var command = ValidCommand();
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
        var command = ValidCommand();
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
        var command = ValidCommand();
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
        var command = ValidCommand();
        command.Sections = (TestSectionScope)(1 << 5);

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditTestCommand.Sections));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.Tests.CountAsync());
    }

    [Fact]
    public async Task Edit_UpdatesScalarFields_WithoutTouchingStatus()
    {
        // Arrange: create a Test.
        var createCommand = ValidCommand();
        Assert.Empty(await ValidateAsync(createCommand));

        var handler = new AddEditTestCommandHandler(CreateFactory(), _mapper);
        var createResult = await handler.Handle(createCommand, CancellationToken.None);
        Assert.True(createResult.Succeeded);

        int testId;
        await using (var context = new ApplicationDbContext(_options))
        {
            testId = await context.Tests.Select(t => t.Id).SingleAsync();
        }

        // Act: edit its scope.
        var editCommand = new AddEditTestCommand
        {
            Id = testId,
            Name = "Renamed Test",
            Codes = LicenceCode.Code1 | LicenceCode.Code2,
            Sections = TestSectionScope.Rules | TestSectionScope.Signs
        };
        Assert.Empty(await ValidateAsync(editCommand));

        var editResult = await handler.Handle(editCommand, CancellationToken.None);

        // Assert
        Assert.True(editResult.Succeeded);

        await using var verifyContext = new ApplicationDbContext(_options);
        var updated = await verifyContext.Tests.SingleAsync(t => t.Id == testId);

        Assert.Equal("Renamed Test", updated.Name);
        Assert.Equal(LicenceCode.Code1 | LicenceCode.Code2, updated.Codes);
        Assert.Equal(TestSectionScope.Rules | TestSectionScope.Signs, updated.Sections);

        // Editing a Test must never change its Status.
        Assert.Equal(TestStatus.Draft, updated.Status);
    }
}
