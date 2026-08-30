using System;
using System.Threading;
using System.Threading.Tasks;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.Tests.Commands.Publish;
using K53Guru.Application.Features.Tests.Commands.Unpublish;
using K53Guru.Domain.Entities;
using K53Guru.Domain.Enums;
using K53Guru.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Features.Tests;

/// <summary>
/// Covers spec-2-3-publish-unpublish-test.md's I/O &amp; Edge-Case Matrix rows directly against the
/// production PublishTestCommandHandler/UnpublishTestCommandHandler, mirroring
/// AddEditTestCommandHandlerTests.cs's SQLite in-memory harness (no live MSSQL/PostgreSQL instance
/// is reachable in this sandbox).
///
/// Matrix rows covered:
///   - Publish, valid (Draft -&gt; Published)
///   - Publish, already published (rejected, status unchanged)
///   - Publish, not found (rejected)
///   - Unpublish, valid (Published -&gt; Draft)
///   - Unpublish, already draft (rejected, status unchanged)
///   - Unpublish, not found (rejected)
/// </summary>
public class PublishUnpublishTestCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public PublishUnpublishTestCommandHandlerTests()
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

    private async Task<int> SeedTestAsync(TestStatus status)
    {
        await using var context = new ApplicationDbContext(_options);
        var test = new Test
        {
            Name = "Sample Test",
            Codes = LicenceCode.Code1,
            Sections = TestSectionScope.Rules,
            Status = status
        };
        context.Tests.Add(test);
        await context.SaveChangesAsync();
        return test.Id;
    }

    private async Task<TestStatus> GetStatusAsync(int id)
    {
        await using var context = new ApplicationDbContext(_options);
        return await context.Tests.Where(t => t.Id == id).Select(t => t.Status).SingleAsync();
    }

    [Fact]
    public async Task Publish_Valid_DraftBecomesPublished()
    {
        // Arrange
        var testId = await SeedTestAsync(TestStatus.Draft);
        var handler = new PublishTestCommandHandler(CreateFactory());

        // Act
        var result = await handler.Handle(new PublishTestCommand { Id = testId }, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(TestStatus.Published, await GetStatusAsync(testId));
    }

    [Fact]
    public async Task Publish_AlreadyPublished_RejectedWithClearMessage_StatusUnchanged()
    {
        // Arrange
        var testId = await SeedTestAsync(TestStatus.Published);
        var handler = new PublishTestCommandHandler(CreateFactory());

        // Act
        var result = await handler.Handle(new PublishTestCommand { Id = testId }, CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("already published", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TestStatus.Published, await GetStatusAsync(testId));
    }

    [Fact]
    public async Task Publish_NotFound_RejectedWithClearMessage()
    {
        // Arrange
        var handler = new PublishTestCommandHandler(CreateFactory());

        // Act
        var result = await handler.Handle(new PublishTestCommand { Id = 12345 }, CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unpublish_Valid_PublishedBecomesDraft()
    {
        // Arrange
        var testId = await SeedTestAsync(TestStatus.Published);
        var handler = new UnpublishTestCommandHandler(CreateFactory());

        // Act
        var result = await handler.Handle(new UnpublishTestCommand { Id = testId }, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(TestStatus.Draft, await GetStatusAsync(testId));
    }

    [Fact]
    public async Task Unpublish_AlreadyDraft_RejectedWithClearMessage_StatusUnchanged()
    {
        // Arrange
        var testId = await SeedTestAsync(TestStatus.Draft);
        var handler = new UnpublishTestCommandHandler(CreateFactory());

        // Act
        var result = await handler.Handle(new UnpublishTestCommand { Id = testId }, CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("already", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TestStatus.Draft, await GetStatusAsync(testId));
    }

    [Fact]
    public async Task Unpublish_NotFound_RejectedWithClearMessage()
    {
        // Arrange
        var handler = new UnpublishTestCommandHandler(CreateFactory());

        // Act
        var result = await handler.Handle(new UnpublishTestCommand { Id = 12345 }, CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
