using System.ComponentModel.DataAnnotations;

namespace React_Receiver.Models;

public sealed record GetUserRequest
{
    public GetUserRequest()
    {
    }

    public GetUserRequest(string? userId)
    {
        UserId = userId;
    }

    [Required(AllowEmptyStrings = false)]
    public string? UserId { get; init; }
}
