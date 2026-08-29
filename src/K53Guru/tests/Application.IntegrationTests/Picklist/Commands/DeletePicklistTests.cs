using System.Threading.Tasks;
using K53Guru.Application.Common.ExceptionHandlers;
using K53Guru.Application.Features.PicklistSets.Commands.AddEdit;
using K53Guru.Application.Features.PicklistSets.Commands.Delete;
using K53Guru.Domain.Entities;
using FluentAssertions;
using NUnit.Framework;

namespace K53Guru.Application.IntegrationTests.KeyValues.Commands;

using static Testing;

public class DeletePicklistTests : TestBase
{
    [Test]
    public void ShouldRequireValidKeyValueId()
    {
        var command = new DeletePicklistSetCommand(new[] { 99 });

        FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ShouldDeleteKeyValue()
    {
        var addCommand = new AddEditPicklistSetCommand
        {
            Name = Picklist.Brand,
            Text = "Word",
            Value = "Word",
            Description = "For Test"
        };
        var result = await SendAsync(addCommand);

        await SendAsync(new DeletePicklistSetCommand(new[] { result.Data }));

        var item = await FindAsync<Document>(result.Data);

        item.Should().BeNull();
    }
}
