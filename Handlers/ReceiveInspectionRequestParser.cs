using System.Text.Json;
using Microsoft.AspNetCore.Http;
using React_Receiver.Models;

namespace React_Receiver.Handlers;

public interface IReceiveInspectionRequestParser
{
    bool TryParseFormRequest(
        string? payload,
        IFormFile[]? files,
        out ReceiveInspectionRequest request);
}

public sealed class ReceiveInspectionRequestParser : IReceiveInspectionRequestParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool TryParseFormRequest(
        string? payload,
        IFormFile[]? files,
        out ReceiveInspectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            request = new ReceiveInspectionRequest(
                SessionId: null,
                UserId: null,
                Name: null,
                QueryParams: null,
                Files: files);
            return true;
        }

        try
        {
            var normalizedPayload = NormalizePayload(payload);
            var parsed = JsonSerializer.Deserialize<ReceiveInspectionRequest>(
                normalizedPayload,
                JsonOptions);
            if (parsed is null)
            {
                request = new ReceiveInspectionRequest(
                    SessionId: null,
                    UserId: null,
                    Name: null,
                    QueryParams: null,
                    Files: files);
                return false;
            }

            request = parsed with { Files = files };
            return true;
        }
        catch (JsonException)
        {
            request = new ReceiveInspectionRequest(
                SessionId: null,
                UserId: null,
                Name: null,
                QueryParams: null,
                Files: files);
            return false;
        }
    }

    private static string NormalizePayload(string payload)
    {
        if (payload.Length >= 2 && payload[0] == '"' && payload[^1] == '"')
        {
            try
            {
                var unwrapped = JsonSerializer.Deserialize<string>(payload, JsonOptions);
                if (!string.IsNullOrWhiteSpace(unwrapped))
                {
                    return unwrapped;
                }
            }
            catch (JsonException)
            {
                return payload;
            }
        }

        return payload;
    }
}
