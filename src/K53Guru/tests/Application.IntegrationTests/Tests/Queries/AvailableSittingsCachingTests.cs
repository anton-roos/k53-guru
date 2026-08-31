using System.Linq;
using System.Threading.Tasks;
using K53Guru.Application.Features.Tests.Commands.Publish;
using K53Guru.Application.Features.Tests.Queries.AvailableSittings;
using K53Guru.Domain.Entities;
using K53Guru.Domain.Enums;
using NUnit.Framework;

namespace K53Guru.Application.IntegrationTests.Tests.Queries;

using static Testing;

/// <summary>
/// Exercises spec-3-1-discover-available-sittings.md's caching + invalidation wiring through the
/// REAL, DI-wired MediatR pipeline (mirrors AuthorizationTests.cs's approach for Story 2.5) -
/// sending the actual, production <see cref="GetAvailableSittingsQuery"/> (FusionCache-backed via
/// ICacheableRequest) and <see cref="PublishTestCommand"/> (tag-invalidating via
/// ICacheInvalidatorRequest) via <c>SendAsync</c>. Every other test covering this story's query
/// calls GetAvailableSittingsQueryHandler directly, bypassing the pipeline where
/// FusionCacheBehaviour/CacheInvalidationBehaviour actually run - so nothing else would catch a
/// cache-tag mismatch that leaves a stale, pre-publish (or pre-unpublish) result cached forever.
///
/// NOTE: as with AuthorizationTests.cs, this project cannot execute in this sandbox due to a
/// pre-existing MSSQL migration-drift issue (logged in deferred-work.md, confirmed at commit
/// cead7c0) unrelated to this story. This test is written to be correct by code review; its
/// pass/fail status could not be verified in this environment.
/// </summary>
internal class AvailableSittingsCachingTests : TestBase
{
    private static async Task<int> SeedDraftTestAsync()
    {
        var test = new Test
        {
            Name = "Available sittings caching test",
            Codes = LicenceCode.Code1,
            Sections = TestSectionScope.Rules,
            Status = TestStatus.Draft
        };
        await AddAsync(test);
        return test.Id;
    }

    [Test]
    public async Task PublishTestCommand_InvalidatesCache_SoAvailableSittingsReflectsThePublish()
    {
        await RunAsAdministratorAsync();
        var testId = await SeedDraftTestAsync();

        // The draft Test must not appear in the (now cache-populating) discovery query.
        var beforePublish = await SendAsync(new GetAvailableSittingsQuery());
        Assert.That(beforePublish.Any(s => s.Id == testId), Is.False);

        var publishResult = await SendAsync(new PublishTestCommand { Id = testId });
        Assert.That(publishResult.Succeeded, Is.True);

        // If the publish's tag-based invalidation didn't actually evict the cache entry
        // populated above, this would still return the stale pre-publish (empty-of-testId) result.
        var afterPublish = await SendAsync(new GetAvailableSittingsQuery());
        Assert.That(afterPublish.Any(s => s.Id == testId), Is.True);
    }
}
