namespace React_Receiver.Models;

public sealed record RegisterResponseModel(
    string UserId,
    int FileCount,
    string[] UploadedBlobs
);
