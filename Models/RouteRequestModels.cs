using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

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

public sealed class GetInspectionIngestOutboxSessionRequest
{
    [Required(AllowEmptyStrings = false)]
    public string? SessionId { get; init; }
}

public sealed class GetInspectionIngestOutboxRequest
{
    public string? Status { get; init; }
    public int Limit { get; init; } = 50;
}

public sealed class ReplayInspectionIngestOutboxRequest
{
    public bool Force { get; init; }
}

public sealed class FormSchemaRouteRequest
{
    [FromRoute(Name = "formType")]
    [Required(AllowEmptyStrings = false)]
    public string? FormType { get; init; }
}

public sealed class TranslationLanguageRequest
{
    [Required(AllowEmptyStrings = false)]
    public string? Language { get; init; }
}

public sealed class TenantConfigRouteRequest
{
    [FromRoute(Name = "tenantId")]
    [Required(AllowEmptyStrings = false)]
    public string? TenantId { get; init; }
}

public sealed class TenantConfigQueryRequest
{
    public string? TenantId { get; init; }
}
