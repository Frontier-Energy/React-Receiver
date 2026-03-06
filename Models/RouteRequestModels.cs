using System.ComponentModel.DataAnnotations;

namespace React_Receiver.Models;

public sealed class GetInspectionRequest
{
    [Required(AllowEmptyStrings = false)]
    public string? SessionId { get; init; }
}

public sealed class GetInspectionFileRequest
{
    [Required(AllowEmptyStrings = false)]
    public string? SessionId { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? FileName { get; init; }
}

public sealed class FormSchemaRouteRequest
{
    [Required(AllowEmptyStrings = false)]
    public string? FormType { get; init; }
}

public sealed class TranslationLanguageRequest
{
    [Required(AllowEmptyStrings = false)]
    public string? Language { get; init; }
}

public sealed class TenantConfigQueryRequest
{
    public string? TenantId { get; init; }
}
