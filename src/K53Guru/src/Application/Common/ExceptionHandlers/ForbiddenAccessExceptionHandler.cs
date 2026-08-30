namespace K53Guru.Application.Common.ExceptionHandlers;

public sealed class
    ForbiddenAccessExceptionHandler<TRequest, TResponse, TException> : IRequestExceptionHandler<TRequest, TResponse,
    TException>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult
    where TException : ForbiddenAccessException
{

    public Task Handle(TRequest request, TException exception, RequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken)
    {
        var failureResult = CreateFailureResult(exception.Message);
        state.SetHandled(failureResult);
        return Task.CompletedTask;
    }

    private TResponse CreateFailureResult(string errorMessage)
    {
        if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            // Get the type parameter T in Result<T>
            var resultType = typeof(TResponse).GetGenericArguments()[0];

            // Use reflection to invoke Result<T>.Failure method
            var failureMethod = typeof(Result<>)
                .MakeGenericType(resultType)
                .GetMethod("Failure", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string[]) }, null);

            if (failureMethod != null)
            {
                return (TResponse)failureMethod.Invoke(null, new object[] { new[] { errorMessage } })!;
            }
        }
        else
        {
            // For non-generic Result type
            var failureMethod = typeof(Result).GetMethod("Failure", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string[]) }, null);
            if (failureMethod != null)
            {
                return (TResponse)failureMethod.Invoke(null, new object[] { new[] { errorMessage } })!;
            }
        }

        throw new InvalidOperationException($"Could not create failure result for type {typeof(TResponse).Name}");
    }
}
