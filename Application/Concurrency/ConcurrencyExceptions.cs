namespace React_Receiver.Application.Concurrency;

public sealed class PreconditionRequiredException : InvalidOperationException
{
    public PreconditionRequiredException(string message)
        : base(message)
    {
    }
}

public sealed class ConcurrencyConflictException : InvalidOperationException
{
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }
}
