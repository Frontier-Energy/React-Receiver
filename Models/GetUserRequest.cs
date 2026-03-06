using System.ComponentModel.DataAnnotations;

namespace React_Receiver.Models;

public sealed record GetUserRequest(
    [property: Required(AllowEmptyStrings = false)] string? UserId
);
