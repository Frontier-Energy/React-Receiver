namespace React_Receiver.Models;

public sealed record UserModel(
    string UserId,
    string? Email,
    string? FirstName,
    string? LastName
);
