using System.ComponentModel.DataAnnotations;

namespace React_Receiver.Models;

public sealed record FormSchemaResponse(
    [property: Required(AllowEmptyStrings = false)] string? FormName,
    [property: Required] FormSectionResponse[]? Sections
);

public sealed record FormSectionResponse(
    [property: Required(AllowEmptyStrings = false)] string? Title,
    [property: Required] FormFieldResponse[]? Fields
);

public sealed record FormFieldResponse(
    [property: Required(AllowEmptyStrings = false)] string? Id,
    [property: Required(AllowEmptyStrings = false)] string? Label,
    [property: Required(AllowEmptyStrings = false)] string? Type,
    bool Required,
    string? ExternalID = null,
    FormFieldOptionResponse[]? Options = null,
    string? Placeholder = null,
    string? Description = null,
    ValidationRuleResponse[]? ValidationRules = null,
    ConditionalVisibilityResponse[]? VisibleWhen = null,
    string? Accept = null,
    bool? Multiple = null,
    string? Capture = null
);

public sealed record FormFieldOptionResponse(
    [property: Required(AllowEmptyStrings = false)] string? Label,
    [property: Required(AllowEmptyStrings = false)] string? Value
);

public sealed record ValidationRuleResponse(
    [property: Required(AllowEmptyStrings = false)] string? Type,
    object? Value,
    [property: Required(AllowEmptyStrings = false)] string? Message
);

public sealed record ConditionalVisibilityResponse(
    [property: Required(AllowEmptyStrings = false)] string? FieldId,
    object? Value,
    string? Operator = null
);
