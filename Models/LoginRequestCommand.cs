using System.ComponentModel.DataAnnotations;

namespace React_Receiver.Models;

public sealed record LoginRequestCommand(
    [property: Required(AllowEmptyStrings = false)]
    [property: EmailAddress]
    string? Email
);
