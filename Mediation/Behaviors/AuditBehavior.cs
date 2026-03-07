using MediatR;
using React_Receiver.Application.Auth;
using React_Receiver.Application.FormSchemas;
using React_Receiver.Application.Inspections;
using React_Receiver.Application.TenantConfig;
using React_Receiver.Application.Translations;
using React_Receiver.Models;
using React_Receiver.Observability;

namespace React_Receiver.Mediation.Behaviors;

public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditEventLogger _auditEventLogger;

    public AuditBehavior(IAuditEventLogger auditEventLogger)
    {
        _auditEventLogger = auditEventLogger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await next();
            LogSuccess(request, response);
            return response;
        }
        catch (Exception ex)
        {
            LogFailure(request, ex);
            throw;
        }
    }

    private void LogSuccess(TRequest request, TResponse response)
    {
        switch (request)
        {
            case LoginCommand command when response is LoginRequestResponse loginResponse:
                _auditEventLogger.Log("auth.login", CreateProperties(
                    ("email", MaskEmail(command.Request.Email)),
                    ("result", string.IsNullOrWhiteSpace(loginResponse.UserId) ? "rejected" : "success"),
                    ("userId", NullIfWhiteSpace(loginResponse.UserId))));
                break;
            case RegisterCommand command when response is RegisterResponseModel registerResponse:
                _auditEventLogger.Log("auth.register", CreateProperties(
                    ("email", MaskEmail(command.Request.Email)),
                    ("userId", NullIfWhiteSpace(registerResponse.UserId)),
                    ("result", "success")));
                break;
            case ReceiveInspectionCommand command when response is ReceiveInspectionResponse inspectionResponse:
                _auditEventLogger.Log("inspection.ingest", CreateProperties(
                    ("sessionId", NullIfWhiteSpace(inspectionResponse.SessionId)),
                    ("inspectionName", NullIfWhiteSpace(inspectionResponse.Name)),
                    ("fileCount", command.Request.Files?.Length ?? 0),
                    ("result", inspectionResponse.Status)));
                break;
            case UpsertTenantConfigCommand command when response is UpsertResult<TenantBootstrapResponse> tenantResponse:
                _auditEventLogger.Log("tenant_config.mutate", CreateProperties(
                    ("tenantId", NullIfWhiteSpace(command.Request.TenantId)),
                    ("created", tenantResponse.Created),
                    ("result", "success")));
                break;
            case UpsertFormSchemaCommand command when response is UpsertResult<FormSchemaResponse> schemaResponse:
                _auditEventLogger.Log("form_schema.mutate", CreateProperties(
                    ("formType", NullIfWhiteSpace(command.FormType)),
                    ("created", schemaResponse.Created),
                    ("version", NullIfWhiteSpace(schemaResponse.Version)),
                    ("result", "success")));
                break;
            case UpsertTranslationsCommand command when response is UpsertResult<TranslationsResponse> translationResponse:
                _auditEventLogger.Log("translations.mutate", CreateProperties(
                    ("language", NullIfWhiteSpace(command.Language)),
                    ("created", translationResponse.Created),
                    ("result", "success")));
                break;
        }
    }

    private void LogFailure(TRequest request, Exception exception)
    {
        switch (request)
        {
            case LoginCommand command:
                _auditEventLogger.Log("auth.login", CreateProperties(
                    ("email", MaskEmail(command.Request.Email)),
                    ("result", "failure"),
                    ("errorType", exception.GetType().Name)));
                break;
            case RegisterCommand command:
                _auditEventLogger.Log("auth.register", CreateProperties(
                    ("email", MaskEmail(command.Request.Email)),
                    ("result", "failure"),
                    ("errorType", exception.GetType().Name)));
                break;
            case ReceiveInspectionCommand command:
                _auditEventLogger.Log("inspection.ingest", CreateProperties(
                    ("fileCount", command.Request.Files?.Length ?? 0),
                    ("result", "failure"),
                    ("errorType", exception.GetType().Name)));
                break;
            case UpsertTenantConfigCommand command:
                _auditEventLogger.Log("tenant_config.mutate", CreateProperties(
                    ("tenantId", NullIfWhiteSpace(command.Request.TenantId)),
                    ("result", "failure"),
                    ("errorType", exception.GetType().Name)));
                break;
            case UpsertFormSchemaCommand command:
                _auditEventLogger.Log("form_schema.mutate", CreateProperties(
                    ("formType", NullIfWhiteSpace(command.FormType)),
                    ("result", "failure"),
                    ("errorType", exception.GetType().Name)));
                break;
            case UpsertTranslationsCommand command:
                _auditEventLogger.Log("translations.mutate", CreateProperties(
                    ("language", NullIfWhiteSpace(command.Language)),
                    ("result", "failure"),
                    ("errorType", exception.GetType().Name)));
                break;
        }
    }

    private static Dictionary<string, object?> CreateProperties(params (string Key, object? Value)[] entries)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (key, value) in entries)
        {
            if (value is not null)
            {
                properties[key] = value;
            }
        }

        return properties;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var parts = email.Split('@', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return "***";
        }

        return $"{parts[0][0]}***@{parts[1]}";
    }
}
