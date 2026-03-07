using System.ComponentModel.DataAnnotations;

namespace React_Receiver.Models;

public sealed record LoginRequestCommand
{
    public LoginRequestCommand()
    {
    }

    public LoginRequestCommand(string? email)
    {
        Email = email;
    }

    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string? Email { get; init; }
}
