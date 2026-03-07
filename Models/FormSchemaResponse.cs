using System.ComponentModel.DataAnnotations;

namespace React_Receiver.Models;

public sealed record FormSchemaResponse
{
    public FormSchemaResponse()
    {
    }

    public FormSchemaResponse(string? FormName, FormSectionResponse[]? Sections)
    {
        this.FormName = FormName;
        this.Sections = Sections;
    }

    [Required(AllowEmptyStrings = false)]
    public string? FormName { get; init; }

    [Required]
    public FormSectionResponse[]? Sections { get; init; }
}

public sealed record FormSectionResponse
{
    public FormSectionResponse()
    {
    }

    public FormSectionResponse(string? Title, FormFieldResponse[]? Fields)
    {
        this.Title = Title;
        this.Fields = Fields;
    }

    [Required(AllowEmptyStrings = false)]
    public string? Title { get; init; }

    [Required]
    public FormFieldResponse[]? Fields { get; init; }
}

public sealed record FormFieldResponse
{
    public FormFieldResponse()
    {
    }

    public FormFieldResponse(
        string? Id,
        string? Label,
        string? Type,
        bool Required,
        string? ExternalID = null,
        FormFieldOptionResponse[]? Options = null,
        string? Placeholder = null,
        string? Description = null,
        ValidationRuleResponse[]? ValidationRules = null,
        ConditionalVisibilityResponse[]? VisibleWhen = null,
        string? Accept = null,
        bool? Multiple = null,
        string? Capture = null)
    {
        this.Id = Id;
        this.Label = Label;
        this.Type = Type;
        this.Required = Required;
        this.ExternalID = ExternalID;
        this.Options = Options;
        this.Placeholder = Placeholder;
        this.Description = Description;
        this.ValidationRules = ValidationRules;
        this.VisibleWhen = VisibleWhen;
        this.Accept = Accept;
        this.Multiple = Multiple;
        this.Capture = Capture;
    }

    [Required(AllowEmptyStrings = false)]
    public string? Id { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? Label { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? Type { get; init; }

    public bool Required { get; init; }

    public string? ExternalID { get; init; }

    public FormFieldOptionResponse[]? Options { get; init; }

    public string? Placeholder { get; init; }

    public string? Description { get; init; }

    public ValidationRuleResponse[]? ValidationRules { get; init; }

    public ConditionalVisibilityResponse[]? VisibleWhen { get; init; }

    public string? Accept { get; init; }

    public bool? Multiple { get; init; }

    public string? Capture { get; init; }
}

public sealed record FormFieldOptionResponse
{
    public FormFieldOptionResponse()
    {
    }

    public FormFieldOptionResponse(string? Label, string? Value)
    {
        this.Label = Label;
        this.Value = Value;
    }

    [Required(AllowEmptyStrings = false)]
    public string? Label { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? Value { get; init; }
}

public sealed record ValidationRuleResponse
{
    public ValidationRuleResponse()
    {
    }

    public ValidationRuleResponse(string? Type, object? Value, string? Message)
    {
        this.Type = Type;
        this.Value = Value;
        this.Message = Message;
    }

    [Required(AllowEmptyStrings = false)]
    public string? Type { get; init; }

    public object? Value { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? Message { get; init; }
}

public sealed record ConditionalVisibilityResponse
{
    public ConditionalVisibilityResponse()
    {
    }

    public ConditionalVisibilityResponse(string? FieldId, object? Value, string? Operator = null)
    {
        this.FieldId = FieldId;
        this.Value = Value;
        this.Operator = Operator;
    }

    [Required(AllowEmptyStrings = false)]
    public string? FieldId { get; init; }

    public object? Value { get; init; }

    public string? Operator { get; init; }
}
