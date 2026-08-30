using System.Threading.Tasks;
using K53Guru.Application.Features.Tests.Commands.Publish;
using K53Guru.Domain.Entities;
using K53Guru.Domain.Enums;
using NUnit.Framework;

namespace K53Guru.Application.IntegrationTests.Tests.Commands;

using static Testing;

/// <summary>
/// Exercises spec-2-5-restrict-content-management-to-admins.md's guard through the REAL,
/// DI-wired MediatR pipeline (not a hand-rolled dummy request/behaviour pair) - sending the
/// actual, production <see cref="PublishTestCommand"/> (decorated with
/// <c>[RequestAuthorize(Roles = Roles.Admin)]</c>) via <c>IMediator.Send</c>. This is the only
/// coverage in the repo that would catch a typo'd role string, or the attribute accidentally
/// applied to the wrong class, on a real guarded command.
/// </summary>
internal class AuthorizationTests : TestBase
{
    private static async Task<int> SeedDraftTestAsync()
    {
        var test = new Test
        {
            Name = "Authorization pipeline test",
            Codes = LicenceCode.Code1,
            Sections = TestSectionScope.Rules,
            Status = TestStatus.Draft
        };
        await AddAsync(test);
        return test.Id;
    }

    [Test]
    public async Task Admin_SendsRealGuardedCommandThroughMediator_Succeeds()
    {
        await RunAsAdministratorAsync();
        var testId = await SeedDraftTestAsync();

        var result = await SendAsync(new PublishTestCommand { Id = testId });

        Assert.That(result.Succeeded, Is.True);

        var persisted = await FindAsync<Test>(testId);
        Assert.That(persisted.Status, Is.EqualTo(TestStatus.Published));
    }

    [Test]
    public async Task NonAdmin_SendsRealGuardedCommandThroughMediator_ReturnsCleanFailureAndPersistsNothing()
    {
        // The Test must be seeded before switching the "current user" to the non-admin, since
        // seeding itself does not go through the guarded command.
        var testId = await SeedDraftTestAsync();
        await RunAsDefaultUserAsync();

        var result = await SendAsync(new PublishTestCommand { Id = testId });

        // ForbiddenAccessExceptionHandler must convert the thrown ForbiddenAccessException into a
        // clean Result.Failure - never an unhandled exception escaping SendAsync.
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Is.Not.Empty);

        var persisted = await FindAsync<Test>(testId);
        Assert.That(persisted.Status, Is.EqualTo(TestStatus.Draft));
    }
}
