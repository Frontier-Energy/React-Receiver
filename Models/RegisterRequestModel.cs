using System.ComponentModel.DataAnnotations;

namespace React_Receiver.Models;

public sealed record RegisterRequestModel(
    [property: Required(AllowEmptyStrings = false)]
    [property: EmailAddress]
    string? Email,
    [property: Required(AllowEmptyStrings = false)]
    string? FirstName,
    [property: Required(AllowEmptyStrings = false)]
    string? LastName
);
