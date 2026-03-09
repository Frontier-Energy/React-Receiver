namespace React_Receiver.Mediation.Exceptions;

public sealed class DuplicateInspectionSessionException : Exception
{
    public DuplicateInspectionSessionException(string message)
        : base(message)
    {
    }
}
