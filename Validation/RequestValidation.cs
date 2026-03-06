using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using React_Receiver.Models;

namespace React_Receiver.Validation;

public interface IRequestValidator
{
    bool CanValidate(Type requestType);
    void Validate(object request, ModelStateDictionary modelState);
}

public abstract class RequestValidator<TRequest> : IRequestValidator
{
    public bool CanValidate(Type requestType)
    {
        return typeof(TRequest).IsAssignableFrom(requestType);
    }

    public void Validate(object request, ModelStateDictionary modelState)
    {
        if (request is not TRequest typedRequest)
        {
            return;
        }

        Validate(typedRequest, modelState);
    }

    public abstract void Validate(TRequest request, ModelStateDictionary modelState);
}

public abstract class NoOpRequestValidator<TRequest> : RequestValidator<TRequest>
{
    public override void Validate(TRequest request, ModelStateDictionary modelState)
    {
    }
}

public sealed class RequestValidationFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly IEnumerable<IRequestValidator> _validators;
    private readonly ProblemDetailsFactory _problemDetailsFactory;

    public RequestValidationFilter(
        IEnumerable<IRequestValidator> validators,
        ProblemDetailsFactory problemDetailsFactory)
    {
        _validators = validators;
        _problemDetailsFactory = problemDetailsFactory;
    }

    public int Order => -3000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = CreateValidationProblem(context);
            return;
        }

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            foreach (var validator in _validators.Where(v => v.CanValidate(argument.GetType())))
            {
                validator.Validate(argument, context.ModelState);
            }
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = CreateValidationProblem(context);
            return;
        }

        await next();
    }

    private ActionResult CreateValidationProblem(ActionContext context)
    {
        var details = _problemDetailsFactory.CreateValidationProblemDetails(
            context.HttpContext,
            context.ModelState);

        return new BadRequestObjectResult(details);
    }
}

public static class RequestValidationServiceCollectionExtensions
{
    public static IServiceCollection AddRequestValidation(this IServiceCollection services)
    {
        services.AddSingleton<IRequestValidator, LoginRequestCommandValidator>();
        services.AddSingleton<IRequestValidator, RegisterRequestModelValidator>();
        services.AddSingleton<IRequestValidator, ReceiveInspectionRequestValidator>();
        services.AddSingleton<IRequestValidator, ReceiveInspectionFormRequestValidator>();
        services.AddSingleton<IRequestValidator, GetUserRequestValidator>();
        services.AddSingleton<IRequestValidator, GetInspectionRequestValidator>();
        services.AddSingleton<IRequestValidator, GetInspectionFileRequestValidator>();
        services.AddSingleton<IRequestValidator, FormSchemaRouteRequestValidator>();
        services.AddSingleton<IRequestValidator, TranslationLanguageRequestValidator>();
        services.AddSingleton<IRequestValidator, TenantConfigQueryRequestValidator>();
        services.AddSingleton<IRequestValidator, TenantBootstrapResponseValidator>();
        services.AddSingleton<IRequestValidator, FormSchemaResponseValidator>();
        services.AddSingleton<IRequestValidator, TranslationsResponseValidator>();
        return services;
    }
}

public sealed class LoginRequestCommandValidator : NoOpRequestValidator<LoginRequestCommand>;
public sealed class RegisterRequestModelValidator : NoOpRequestValidator<RegisterRequestModel>;
public sealed class ReceiveInspectionRequestValidator : NoOpRequestValidator<ReceiveInspectionRequest>;
public sealed class ReceiveInspectionFormRequestValidator : NoOpRequestValidator<ReceiveInspectionFormRequest>;
public sealed class GetUserRequestValidator : NoOpRequestValidator<GetUserRequest>;
public sealed class GetInspectionRequestValidator : NoOpRequestValidator<GetInspectionRequest>;
public sealed class GetInspectionFileRequestValidator : NoOpRequestValidator<GetInspectionFileRequest>;
public sealed class FormSchemaRouteRequestValidator : NoOpRequestValidator<FormSchemaRouteRequest>;
public sealed class TranslationLanguageRequestValidator : NoOpRequestValidator<TranslationLanguageRequest>;
public sealed class TenantConfigQueryRequestValidator : NoOpRequestValidator<TenantConfigQueryRequest>;
public sealed class TenantBootstrapResponseValidator : NoOpRequestValidator<TenantBootstrapResponse>;
public sealed class FormSchemaResponseValidator : NoOpRequestValidator<FormSchemaResponse>;
public sealed class TranslationsResponseValidator : NoOpRequestValidator<TranslationsResponse>;
