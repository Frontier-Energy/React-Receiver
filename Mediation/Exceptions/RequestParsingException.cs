namespace React_Receiver.Mediation.Exceptions;

public sealed class RequestParsingException : Exception
{
    public RequestParsingException(string message)
        : base(message)
    {
    }
}
