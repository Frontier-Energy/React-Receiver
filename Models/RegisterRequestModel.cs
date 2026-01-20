namespace React_Receiver.Models;

public sealed record RegisterRequestModel(
    string? Email,
    string? FirstName,
    string? LastName
);
