namespace React_Receiver.Models;

public sealed record MeResponse(
    string UserId,
    string[] Roles,
    string[] Permissions
);
