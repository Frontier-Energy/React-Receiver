using System.Text.Json;
using Microsoft.AspNetCore.Http;
using React_Receiver.Models;

namespace React_Receiver.Handlers;

public interface IReceiveInspectionRequestParser
{
    bool TryParseFormRequest(
        string? payload,
        IFormFile[]? files,
        out ReceiveInspectionRequest request,
        out string? errorMessage);
}

public sealed class ReceiveInspectionRequestParser : IReceiveInspectionRequestParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool TryParseFormRequest(
        string? payload,
        IFormFile[]? files,
        out ReceiveInspectionRequest request,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            request = CreateEmptyRequest(files);
            errorMessage = null;
            return true;
        }

        if (ReceiveInspectionFormRequest.GetPayloadSizeBytes(payload) > ReceiveInspectionFormRequest.MaxPayloadBytes)
        {
            request = CreateEmptyRequest(files);
            errorMessage = $"Payload exceeds the {ReceiveInspectionFormRequest.MaxPayloadBytes} byte limit.";
            return false;
        }

        try
        {
            var normalizedPayload = NormalizePayload(payload);
            if (ReceiveInspectionFormRequest.GetPayloadSizeBytes(normalizedPayload) > ReceiveInspectionFormRequest.MaxPayloadBytes)
            {
                request = CreateEmptyRequest(files);
                errorMessage = $"Payload exceeds the {ReceiveInspectionFormRequest.MaxPayloadBytes} byte limit.";
                return false;
            }

            var parsed = JsonSerializer.Deserialize<ReceiveInspectionRequest>(
                normalizedPayload,
                JsonOptions);
            if (parsed is null)
            {
                request = CreateEmptyRequest(files);
                errorMessage = "Payload JSON could not be parsed.";
                return false;
            }

            request = parsed with { Files = files };
            errorMessage = null;
            return true;
        }
        catch (JsonException)
        {
            request = CreateEmptyRequest(files);
            errorMessage = "Payload must be valid JSON.";
            return false;
        }
    }

    private static ReceiveInspectionRequest CreateEmptyRequest(IFormFile[]? files)
    {
        return new ReceiveInspectionRequest(
            SessionId: null,
            UserId: null,
            Name: null,
            QueryParams: null,
            Files: files);
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
