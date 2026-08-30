using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using K53Guru.Application.Common.Constants;
using K53Guru.Application.Common.ExceptionHandlers;
using K53Guru.Application.Common.Interfaces.Identity;
using K53Guru.Application.Common.Models;
using K53Guru.Application.Common.Security;
using K53Guru.Application.Pipeline;
using MediatR;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Pipeline;

/// <summary>
/// Covers spec-2-5-restrict-content-management-to-admins.md's I/O &amp; Edge-Case Matrix rows
/// directly against the production AuthorizationBehaviour, constructed with a fake
/// IUserContextAccessor and dummy decorated/undecorated request types - no DbContext needed.
///
/// Matrix rows covered:
///   - Admin sends a guarded command -> proceeds to the handler exactly as before
///   - Non-admin sends a guarded command -> rejected before the handler runs; nothing persisted
///   - No user context sends a guarded command -> rejected before the handler runs
///   - Unguarded command sent by anyone -> proceeds exactly as before this story
/// </summary>
public class AuthorizationBehaviourTests
{
    [RequestAuthorize(Roles = Roles.Admin)]
    public class GuardedCommand : IRequest<Result>
    {
    }

    [RequestAuthorize]
    public class GuardedCommandNoRolesSpecified : IRequest<Result>
    {
    }

    public class UnguardedCommand : IRequest<Result>
    {
    }

    private static IUserContextAccessor CreateAccessor(IReadOnlyList<string>? roles)
    {
        var accessor = new Mock<IUserContextAccessor>();
        var context = roles == null
            ? null
            : new UserContext("user-1", "user-1", Roles: roles);
        accessor.Setup(a => a.Current).Returns(context);
        return accessor.Object;
    }

    [Fact]
    public async Task Admin_SendsGuardedCommand_ProceedsToHandlerExactlyAsBefore()
    {
        // Arrange
        var behaviour = new AuthorizationBehaviour<GuardedCommand, Result>(CreateAccessor(new[] { Roles.Admin }));
        var nextCalled = false;
        Result? expectedInstance = null;
        RequestHandlerDelegate<Result> next = _ =>
        {
            nextCalled = true;
            expectedInstance = Result.Success();
            return Task.FromResult(expectedInstance);
        };

        // Act
        var result = await behaviour.Handle(new GuardedCommand(), next, CancellationToken.None);

        // Assert
        Assert.True(nextCalled);
        Assert.True(result.Succeeded);
        Assert.Same(expectedInstance, result);
    }

    [Fact]
    public async Task NonAdmin_SendsGuardedCommand_RejectedBeforeHandlerRuns()
    {
        // Arrange
        var behaviour = new AuthorizationBehaviour<GuardedCommand, Result>(CreateAccessor(new[] { Roles.Basic }));
        var nextCalled = false;
        RequestHandlerDelegate<Result> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            behaviour.Handle(new GuardedCommand(), next, CancellationToken.None));

        Assert.False(nextCalled);
        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public async Task NoUserContext_SendsGuardedCommand_RejectedBeforeHandlerRuns()
    {
        // Arrange
        var behaviour = new AuthorizationBehaviour<GuardedCommand, Result>(CreateAccessor(null));
        var nextCalled = false;
        RequestHandlerDelegate<Result> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            behaviour.Handle(new GuardedCommand(), next, CancellationToken.None));

        Assert.False(nextCalled);
        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public async Task NoUserContext_SendsGuardedCommandWithNoRolesSpecified_RejectedBeforeHandlerRuns()
    {
        // Arrange - a [RequestAuthorize] attribute with no Roles set must still require
        // authentication at minimum, rather than silently letting an unauthenticated caller
        // through as if the command had no attribute at all.
        var behaviour = new AuthorizationBehaviour<GuardedCommandNoRolesSpecified, Result>(CreateAccessor(null));
        var nextCalled = false;
        RequestHandlerDelegate<Result> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            behaviour.Handle(new GuardedCommandNoRolesSpecified(), next, CancellationToken.None));

        Assert.False(nextCalled);
    }

    [Fact]
    public async Task UnguardedCommand_SentByAnyone_ProceedsCompletelyUnchanged()
    {
        // Arrange - no user context at all, yet the undecorated command must still pass through.
        var behaviour = new AuthorizationBehaviour<UnguardedCommand, Result>(CreateAccessor(null));
        var nextCalled = false;
        Result? expectedInstance = null;
        RequestHandlerDelegate<Result> next = _ =>
        {
            nextCalled = true;
            expectedInstance = Result.Success();
            return Task.FromResult(expectedInstance);
        };

        // Act
        var result = await behaviour.Handle(new UnguardedCommand(), next, CancellationToken.None);

        // Assert
        Assert.True(nextCalled);
        Assert.True(result.Succeeded);
        Assert.Same(expectedInstance, result);
    }
}
