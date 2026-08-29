using System.Linq;
using System.Threading.Tasks;
using K53Guru.Application.Common.ExceptionHandlers;
using K53Guru.Application.Features.Products.Commands.AddEdit;
using K53Guru.Application.Features.Products.Commands.Delete;
using K53Guru.Application.Features.Products.Queries.GetAll;
using K53Guru.Domain.Entities;
using FluentAssertions;
using NUnit.Framework;

namespace K53Guru.Application.IntegrationTests.Products.Commands;

using static Testing;

internal class DeleteProductCommandTests : TestBase
{
    [Test]
    public void ShouldRequireValidKeyValueId()
    {
        var command = new DeleteProductCommand(new[] { 99 });

        FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ShouldDeleteOne()
    {
        var addCommand = new AddEditProductCommand
            { Name = "Test", Brand = "Brand", Price = 100m, Unit = "EA", Description = "Description" };
        var result = await SendAsync(addCommand);

        await SendAsync(new DeleteProductCommand(new[] { result.Data }));

        var item = await FindAsync<Product>(result.Data);

        item.Should().BeNull();
    }

    [SetUp]
    public async Task InitData()
    {
        await AddAsync(new Product { Name = "Test1" });
        await AddAsync(new Product { Name = "Test2" });
        await AddAsync(new Product { Name = "Test3" });
        await AddAsync(new Product { Name = "Test4" });
    }

    [Test]
    public async Task ShouldDeleteAll()
    {
        var query = new GetAllProductsQuery();
        var result = await SendAsync(query);
        result.Count().Should().Be(4);
        var id = result.Select(x => x.Id).ToArray();
        var deleted = await SendAsync(new DeleteProductCommand(id));
        deleted.Succeeded.Should().BeTrue();

        var deleteResult = await SendAsync(query);
        deleteResult.Should().BeNullOrEmpty();
    }
}
