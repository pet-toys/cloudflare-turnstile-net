using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace PetToys.CloudflareTurnstileNet;

internal sealed class ValidateTurnstileFilter(
    string formField,
    string formErrorMessage,
    string? fieldErrorMessage,
    bool useRemoteIp,
    bool useIdempotencyKey)
    : IAsyncActionFilter, IAsyncPageFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!IsSafeMethod(context.HttpContext.Request))
        {
            await ValidateRecaptcha(context).ConfigureAwait(false);
        }

        await next().ConfigureAwait(false);
    }

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (!IsSafeMethod(context.HttpContext.Request))
        {
            await ValidateRecaptcha(context).ConfigureAwait(false);
        }

        await next().ConfigureAwait(false);
    }

    [ExcludeFromCodeCoverage]
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
    {
        return Task.CompletedTask;
    }

    // A safe method carries no form post to verify, so verifying it can only fail -- and on a
    // controller-scoped attribute it would demand a token from every GET the controller serves.
    private static bool IsSafeMethod(HttpRequest request)
    {
        return HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method);
    }

    private static string GetErrorMessage(ActionContext context, string message)
    {
        var localizerFactory = context.HttpContext.RequestServices.GetService<IStringLocalizerFactory>();
        if (localizerFactory is null) return message;

        var localizer = context.ActionDescriptor switch
        {
            ControllerActionDescriptor controllerActionDescriptor => localizerFactory.Create(
                controllerActionDescriptor.ControllerTypeInfo),
            CompiledPageActionDescriptor pageActionDescriptor => localizerFactory.Create(
                pageActionDescriptor.HandlerTypeInfo),
            _ => null,
        };

        return localizer != null ? localizer[message] : message;
    }

    private async Task ValidateRecaptcha(ActionContext context)
    {
        if (context.HttpContext.RequestServices.GetService<IOptionsSnapshot<CloudflareTurnstileOptions>>()?.Value.Enabled != true) return;

        if (!context.HttpContext.Request.HasFormContentType)
        {
            context.ModelState.AddModelError(string.Empty, GetErrorMessage(context, formErrorMessage));
        }
        else
        {
            var service = context.HttpContext.RequestServices.GetRequiredService<ITurnstileService>();

            if (!context.HttpContext.Request.Form.TryGetValue(formField, out var token)
                ||
                !await service.VerifyAsync(
                    token.ToString(),
                    useRemoteIp ? context.HttpContext.Connection.RemoteIpAddress : null,
                    useIdempotencyKey,
                    context.HttpContext.RequestAborted).ConfigureAwait(false))
            {
                context.ModelState.AddModelError(string.Empty, GetErrorMessage(context, formErrorMessage));

                if (fieldErrorMessage is not null)
                {
                    context.ModelState.AddModelError(formField, GetErrorMessage(context, fieldErrorMessage));
                }
            }
        }
    }
}
