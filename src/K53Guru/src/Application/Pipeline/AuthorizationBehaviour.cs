using K53Guru.Application.Common.Interfaces.Identity;

namespace K53Guru.Application.Pipeline;

/// <summary>
///     Gates <c>[RequestAuthorize]</c>-decorated commands/queries at the MediatR pipeline level:
///     a request with no <see cref="RequestAuthorizeAttribute"/> passes through untouched; a
///     decorated request requires the current user to be present and hold at least one of the
///     roles named by the attribute(s), otherwise it is rejected with a
///     <see cref="ForbiddenAccessException"/> before the handler ever runs.
/// </summary>
/// <typeparam name="TRequest">Type of the Request</typeparam>
/// <typeparam name="TResponse">Type of the Response</typeparam>
public class AuthorizationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult
{
    private readonly IUserContextAccessor _userContextAccessor;

    public AuthorizationBehaviour(IUserContextAccessor userContextAccessor)
    {
        _userContextAccessor = userContextAccessor;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var authorizeAttributes = typeof(TRequest).GetCustomAttributes<RequestAuthorizeAttribute>().ToList();

        if (authorizeAttributes.Count == 0)
        {
            return await next().ConfigureAwait(false);
        }

        var requiredRoles = authorizeAttributes
            .SelectMany(a => a.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();

        // At least one [RequestAuthorize] attribute is present, so the caller must be
        // authenticated at minimum - even if no attribute specifies Roles. Without this, a bare
        // [RequestAuthorize] with an empty/whitespace Roles (the attribute's own default) would
        // make requiredRoles.Count == 0 and silently let an unauthenticated caller through,
        // identical to having no attribute at all.
        if (_userContextAccessor.Current == null)
        {
            throw new ForbiddenAccessException("User is not authorized to perform this action.");
        }

        if (requiredRoles.Count > 0)
        {
            var currentUserRoles = _userContextAccessor.Current.Roles;
            var isAuthorized = currentUserRoles != null &&
                                requiredRoles.Any(role => currentUserRoles.Contains(role, StringComparer.OrdinalIgnoreCase));

            if (!isAuthorized)
            {
                throw new ForbiddenAccessException(
                    $"User is not authorized to perform this action. Required role(s): {string.Join(", ", requiredRoles)}.");
            }
        }

        return await next().ConfigureAwait(false);
    }
}
