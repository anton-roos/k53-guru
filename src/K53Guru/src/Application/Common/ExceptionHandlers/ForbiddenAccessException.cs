namespace K53Guru.Application.Common.ExceptionHandlers;

public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException()
        : base("You are not authorized to perform this action.")
    {
    }

    public ForbiddenAccessException(string message)
        : base(message)
    {
    }
}
