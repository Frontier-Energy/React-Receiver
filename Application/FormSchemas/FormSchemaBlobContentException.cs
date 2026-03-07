namespace React_Receiver.Application.FormSchemas;

public sealed class FormSchemaBlobContentException : InvalidOperationException
{
    public FormSchemaBlobContentException(string message)
        : base(message)
    {
    }

    public FormSchemaBlobContentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
