using System.ComponentModel.DataAnnotations;

namespace React_Receiver.Models;

public sealed record RegisterRequestModel
{
    public RegisterRequestModel()
    {
    }

    public RegisterRequestModel(string? email, string? firstName, string? lastName)
    {
        Email = email;
        FirstName = firstName;
        LastName = lastName;
    }

    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string? Email { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? FirstName { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? LastName { get; init; }
}
