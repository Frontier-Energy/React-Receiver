namespace React_Receiver.Models;

public sealed record FormSchemaResponse(
    string FormName,
    FormSectionResponse[] Sections
);

public sealed record FormSectionResponse(
    string Title,
    FormFieldResponse[] Fields
);

public sealed record FormFieldResponse(
    string Id,
    string Label,
    string Type,
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
    string Label,
    string Value
);

public sealed record ValidationRuleResponse(
    string Type,
    object? Value,
    string Message
);

public sealed record ConditionalVisibilityResponse(
    string FieldId,
    object? Value,
    string? Operator = null
);
