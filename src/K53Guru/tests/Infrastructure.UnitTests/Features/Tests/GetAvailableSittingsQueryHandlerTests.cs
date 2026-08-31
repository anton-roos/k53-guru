using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.Tests.Queries.AvailableSittings;
using K53Guru.Domain.Entities;
using K53Guru.Domain.Enums;
using K53Guru.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Features.Tests;

/// <summary>
/// Covers spec-3-1-discover-available-sittings.md's I/O &amp; Edge-Case Matrix data-shape rows
/// directly against the production GetAvailableSittingsQueryHandler, mirroring
/// PublishUnpublishTestCommandHandlerTests.cs's SQLite in-memory harness (no live MSSQL/PostgreSQL
/// instance is reachable in this sandbox). The anonymous-access and rate-limit matrix rows are
/// integration/manual-check concerns covered under the spec's Verification section instead, since
/// no controller-level test harness exists yet in this repo.
///
/// Matrix rows covered:
///   - Published, valid single-code Test exists -> returned
///   - Published, valid combination Test exists -> returned
///   - Published, invalid combination Test exists -> excluded
///   - Draft Test exists -> excluded
///   - No qualifying Test exists -> empty list
/// </summary>
public class GetAvailableSittingsQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IMapper _mapper;

    public GetAvailableSittingsQueryHandlerTests()
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
            new MapperConfiguration(cfg => cfg.AddMaps(Assembly.GetAssembly(typeof(GetAvailableSittingsQuery))));
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

    private async Task SeedAsync(params Test[] tests)
    {
        await using var context = new ApplicationDbContext(_options);
        context.Tests.AddRange(tests);
        await context.SaveChangesAsync();
    }

    private static Test NewTest(string name, TestStatus status, LicenceCode codes) => new()
    {
        Name = name,
        Status = status,
        Codes = codes,
        Sections = TestSectionScope.Rules
    };

    [Fact]
    public async Task PublishedValidSingleCode_IsReturned()
    {
        // Arrange
        var test = NewTest("Code1 Sitting", TestStatus.Published, LicenceCode.Code1);
        await SeedAsync(test);
        var handler = new GetAvailableSittingsQueryHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(new GetAvailableSittingsQuery(), CancellationToken.None);

        // Assert
        var sitting = Assert.Single(result);
        Assert.Equal(test.Id, sitting.Id);
        Assert.Equal("Code1 Sitting", sitting.Name);
        Assert.Equal(LicenceCode.Code1, sitting.Codes);
    }

    [Theory]
    [InlineData(LicenceCode.Code1)]
    [InlineData(LicenceCode.Code2)]
    [InlineData(LicenceCode.Code3)]
    public async Task PublishedValidSingleCode_AnyCode_IsReturned(LicenceCode code)
    {
        // Arrange
        await SeedAsync(NewTest("Single Code Sitting", TestStatus.Published, code));
        var handler = new GetAvailableSittingsQueryHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(new GetAvailableSittingsQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(code, Assert.Single(result).Codes);
    }

    [Theory]
    [InlineData(LicenceCode.Code1 | LicenceCode.Code2)]
    [InlineData(LicenceCode.Code1 | LicenceCode.Code3)]
    public async Task PublishedValidCombination_IsReturned(LicenceCode combo)
    {
        // Arrange
        var test = NewTest("Combo Sitting", TestStatus.Published, combo);
        await SeedAsync(test);
        var handler = new GetAvailableSittingsQueryHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(new GetAvailableSittingsQuery(), CancellationToken.None);

        // Assert
        var sitting = Assert.Single(result);
        Assert.Equal(test.Id, sitting.Id);
        Assert.Equal(combo, sitting.Codes);
    }

    [Fact]
    public async Task PublishedInvalidCombination_Code2Code3_IsExcluded()
    {
        // Arrange
        await SeedAsync(NewTest("Invalid Combo Sitting", TestStatus.Published,
            LicenceCode.Code2 | LicenceCode.Code3));
        var handler = new GetAvailableSittingsQueryHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(new GetAvailableSittingsQuery(), CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task PublishedInvalidCombination_AllThreeCodes_IsExcluded()
    {
        // Arrange
        await SeedAsync(NewTest("All Codes Sitting", TestStatus.Published,
            LicenceCode.Code1 | LicenceCode.Code2 | LicenceCode.Code3));
        var handler = new GetAvailableSittingsQueryHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(new GetAvailableSittingsQuery(), CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task DraftTest_IsExcludedEvenIfCodesAreValid()
    {
        // Arrange
        await SeedAsync(NewTest("Draft Sitting", TestStatus.Draft, LicenceCode.Code1));
        var handler = new GetAvailableSittingsQueryHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(new GetAvailableSittingsQuery(), CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task NoQualifyingTest_ReturnsEmptyList()
    {
        // Arrange
        var handler = new GetAvailableSittingsQueryHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(new GetAvailableSittingsQuery(), CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task MixedRows_ReturnsOnlyPublishedValidCombinations()
    {
        // Arrange
        await SeedAsync(
            NewTest("Published Single", TestStatus.Published, LicenceCode.Code1),
            NewTest("Published Valid Combo", TestStatus.Published, LicenceCode.Code1 | LicenceCode.Code3),
            NewTest("Published Invalid Combo", TestStatus.Published, LicenceCode.Code2 | LicenceCode.Code3),
            NewTest("Draft Single", TestStatus.Draft, LicenceCode.Code2));
        var handler = new GetAvailableSittingsQueryHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(new GetAvailableSittingsQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Name == "Published Single");
        Assert.Contains(result, s => s.Name == "Published Valid Combo");
    }
}
