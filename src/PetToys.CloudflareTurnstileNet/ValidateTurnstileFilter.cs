#nullable enable

using System;
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

namespace PetToys.CloudflareTurnstileNet
{
    internal sealed class ValidateTurnstileFilter(
        ITurnstileService service,
        string formField,
        string formErrorMessage,
        string? fieldErrorMessage)
        : IAsyncActionFilter, IAsyncPageFilter
    {
        private static readonly Func<Type, IStringLocalizerFactory, IStringLocalizer> LocalizerProvider = (type, factory) => factory.Create(type);

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            await ValidateRecaptcha(context);
            await next();
        }

        public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            if (!HttpMethods.IsGet(context.HttpContext.Request.Method)
                && !HttpMethods.IsHead(context.HttpContext.Request.Method)
                && !HttpMethods.IsOptions(context.HttpContext.Request.Method))
            {
                await ValidateRecaptcha(context);
            }

            await next();
        }

        [ExcludeFromCodeCoverage]
        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        {
            return Task.CompletedTask;
        }

        private static string GetErrorMessage(ActionContext context, string message)
        {
            var localizerFactory = context.HttpContext.RequestServices.GetService<IStringLocalizerFactory>();
            if (localizerFactory is null) return message;

            var localizer = context.ActionDescriptor switch
            {
                ControllerActionDescriptor controllerActionDescriptor => LocalizerProvider.Invoke(
                    controllerActionDescriptor.ControllerTypeInfo, localizerFactory),
                CompiledPageActionDescriptor pageActionDescriptor => LocalizerProvider.Invoke(
                    pageActionDescriptor.HandlerTypeInfo, localizerFactory),
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
                if (!context.HttpContext.Request.Form.TryGetValue(formField, out var token)
                    ||
                    !await service.VerifyAsync(token.ToString()))
                {
                    context.ModelState.AddModelError(string.Empty, GetErrorMessage(context, formErrorMessage));

                    if (fieldErrorMessage is not null)
                    {
                        context.ModelState.AddModelError(string.Empty, GetErrorMessage(context, fieldErrorMessage));
                    }
                }
            }
        }
    }
}
