using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using K53Guru.Application.Common.ExceptionHandlers;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.RoadSigns.DTOs;
using K53Guru.Application.Features.RoadSigns.Queries.GetById;
using K53Guru.Application.Features.RoadSigns.Queries.Pagination;
using K53Guru.Domain.Entities;
using K53Guru.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Features.RoadSigns;

/// <summary>
/// Covers spec-1-2-browse-search-sign-catalog.md I/O &amp; Edge-Case Matrix rows directly against
/// the production query handlers, bypassing the DI+Respawn integration harness used by
/// Application.IntegrationTests.RoadSigns.Queries.RoadSignsPaginationQueryTests. That harness
/// requires a live MSSQL/PostgreSQL instance with migrations applied and cannot run in this
/// sandbox (the MSSQL migrator has never had a RoadSign migration added, and no live DB is
/// reachable here for either provider) - the integration test file is kept as documentation of
/// the intended full-stack coverage, exactly as spec-1-1-seed-road-sign-catalog handled the same
/// constraint.
///
/// Uses a shared-connection SQLite in-memory ApplicationDbContext - schema is derived straight
/// from the EF model via EnsureCreated(), sidestepping migration drift between providers entirely
/// - and invokes the real RoadSignsWithPaginationQueryHandler / GetRoadSignByIdQueryHandler
/// production classes through a mocked IApplicationDbContextFactory, the same DI-bypass style as
/// RoadSignSeedingTests.CreateInitializer() in this project.
///
/// Matrix rows covered:
///   - Search by code ("R1" -> only rows whose LegislationCode or Description contains it)
///   - Search by keyword ("stop"/"crossing" -> only rows whose Description contains it)
///   - No match (keyword matches nothing -> empty grid, no error)
///   - Detail view, valid Id -> full LegislationCode, Description, ImageAssetKey returned
///   - Detail view, missing Id -> NotFoundException
/// </summary>
public class RoadSignsQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IMapper _mapper;

    public RoadSignsQueryHandlerTests()
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

        var mapperConfiguration = new MapperConfiguration(cfg => cfg.AddMaps(Assembly.GetAssembly(typeof(RoadSignDto))));
        _mapper = mapperConfiguration.CreateMapper();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task SearchByCode_KeywordMatchesLegislationCode_ReturnsOnlyMatchingRow()
    {
        // Arrange
        await SeedAsync(
            new RoadSign { LegislationCode = "R1", Description = "Stop", ImageAssetKey = "signs/r1.png" },
            new RoadSign { LegislationCode = "R2", Description = "Yield", ImageAssetKey = "signs/r2.png" },
            new RoadSign { LegislationCode = "W101", Description = "Pedestrian crossing ahead", ImageAssetKey = "signs/w101.png" });

        var handler = new RoadSignsWithPaginationQueryHandler(CreateFactory(), _mapper);
        var query = new RoadSignsWithPaginationQuery { Keyword = "R1" };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalItems);
        Assert.Equal("R1", result.Items.Single().LegislationCode);
    }

    [Fact]
    public async Task SearchByKeyword_KeywordMatchesDescription_ReturnsOnlyMatchingRow()
    {
        // Arrange
        await SeedAsync(
            new RoadSign { LegislationCode = "R1", Description = "Stop", ImageAssetKey = "signs/r1.png" },
            new RoadSign { LegislationCode = "R2", Description = "Yield", ImageAssetKey = "signs/r2.png" },
            new RoadSign { LegislationCode = "W101", Description = "Pedestrian crossing ahead", ImageAssetKey = "signs/w101.png" });

        var handler = new RoadSignsWithPaginationQueryHandler(CreateFactory(), _mapper);
        var query = new RoadSignsWithPaginationQuery { Keyword = "crossing" };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalItems);
        Assert.Equal("W101", result.Items.Single().LegislationCode);
    }

    [Fact]
    public async Task Search_KeywordMatchesNothing_ReturnsEmptyGridWithoutError()
    {
        // Arrange
        await SeedAsync(
            new RoadSign { LegislationCode = "R1", Description = "Stop", ImageAssetKey = "signs/r1.png" },
            new RoadSign { LegislationCode = "R2", Description = "Yield", ImageAssetKey = "signs/r2.png" });

        var handler = new RoadSignsWithPaginationQueryHandler(CreateFactory(), _mapper);
        var query = new RoadSignsWithPaginationQuery { Keyword = "no-such-sign" };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(0, result.TotalItems);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetById_ValidId_ReturnsFullDetail()
    {
        // Arrange
        await SeedAsync(new RoadSign { LegislationCode = "R1", Description = "Stop", ImageAssetKey = "signs/r1.png" });

        int id;
        await using (var context = new ApplicationDbContext(_options))
        {
            id = await context.RoadSigns.Select(s => s.Id).SingleAsync();
        }

        var handler = new GetRoadSignByIdQueryHandler(CreateFactory(), _mapper);

        // Act
        var result = await handler.Handle(new GetRoadSignByIdQuery { Id = id }, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("R1", result.Data!.LegislationCode);
        Assert.Equal("Stop", result.Data!.Description);
        Assert.Equal("signs/r1.png", result.Data!.ImageAssetKey);
    }

    [Fact]
    public async Task GetById_MissingId_ThrowsNotFoundException()
    {
        // Arrange
        await SeedAsync(new RoadSign { LegislationCode = "R1", Description = "Stop", ImageAssetKey = "signs/r1.png" });
        var handler = new GetRoadSignByIdQueryHandler(CreateFactory(), _mapper);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetRoadSignByIdQuery { Id = int.MaxValue }, CancellationToken.None));
    }

    private async Task SeedAsync(params RoadSign[] signs)
    {
        await using var context = new ApplicationDbContext(_options);
        context.RoadSigns.AddRange(signs);
        await context.SaveChangesAsync();
    }

    private IApplicationDbContextFactory CreateFactory()
    {
        var factoryMock = new Mock<IApplicationDbContextFactory>();
        factoryMock
            .Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (IApplicationDbContext)new ApplicationDbContext(_options));
        return factoryMock.Object;
    }
}
