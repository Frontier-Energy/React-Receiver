namespace React_Receiver.Models;

public sealed record ReceiveInspectionResponse(
        string Status,
        string SessionId,
        string Name,
        Dictionary<string, string> QueryParams,  
        string Message
      );
