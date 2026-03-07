namespace React_Receiver.Domain.Users;

public sealed record UserProfile(
    string UserId,
    string? Email,
    string? FirstName,
    string? LastName
);

public sealed record CurrentUser(
    string UserId,
    string[] Roles,
    string[] Permissions
);
