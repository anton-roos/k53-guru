using System.Linq;
using System.Threading.Tasks;
using K53Guru.Application.Features.RoadSigns.Queries.GetById;
using K53Guru.Application.Features.RoadSigns.Queries.Pagination;
using K53Guru.Domain.Entities;
using NUnit.Framework;

namespace K53Guru.Application.IntegrationTests.RoadSigns.Queries;

using static Testing;

internal class RoadSignsPaginationQueryTests : TestBase
{
    [SetUp]
    public async Task InitData()
    {
        await AddAsync(new RoadSign { LegislationCode = "R1", Description = "Stop", ImageAssetKey = "signs/r1.png" });
        await AddAsync(new RoadSign { LegislationCode = "R2", Description = "Yield", ImageAssetKey = "signs/r2.png" });
        await AddAsync(new RoadSign { LegislationCode = "W101", Description = "Pedestrian crossing ahead", ImageAssetKey = "signs/w101.png" });
    }

    [Test]
    public async Task ShouldReturnAllRoadSignsWhenNoKeyword()
    {
        var query = new RoadSignsWithPaginationQuery();
        var result = await SendAsync(query);
        Assert.That(result.TotalItems, Is.EqualTo(3));
    }

    [Test]
    public async Task ShouldFilterByLegislationCode()
    {
        var query = new RoadSignsWithPaginationQuery { Keyword = "R1" };
        var result = await SendAsync(query);
        Assert.That(result.TotalItems, Is.EqualTo(1));
        Assert.That(result.Items.First().LegislationCode, Is.EqualTo("R1"));
    }

    [Test]
    public async Task ShouldFilterByDescriptionKeyword()
    {
        var query = new RoadSignsWithPaginationQuery { Keyword = "crossing" };
        var result = await SendAsync(query);
        Assert.That(result.TotalItems, Is.EqualTo(1));
        Assert.That(result.Items.First().LegislationCode, Is.EqualTo("W101"));
    }

    [Test]
    public async Task ShouldReturnEmptyWhenNoMatch()
    {
        var query = new RoadSignsWithPaginationQuery { Keyword = "no-such-sign" };
        var result = await SendAsync(query);
        Assert.That(result.TotalItems, Is.EqualTo(0));
    }

    [Test]
    public async Task ShouldReturnFullDetailForValidId()
    {
        var listResult = await SendAsync(new RoadSignsWithPaginationQuery { Keyword = "R1" });
        var id = listResult.Items.First().Id;

        var result = await SendAsync(new GetRoadSignByIdQuery { Id = id });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data!.LegislationCode, Is.EqualTo("R1"));
        Assert.That(result.Data!.Description, Is.EqualTo("Stop"));
        Assert.That(result.Data!.ImageAssetKey, Is.EqualTo("signs/r1.png"));
    }

    [Test]
    public async Task ShouldFailWithNotFoundForMissingId()
    {
        var result = await SendAsync(new GetRoadSignByIdQuery { Id = int.MaxValue });

        Assert.That(result.Succeeded, Is.False);
    }
}
