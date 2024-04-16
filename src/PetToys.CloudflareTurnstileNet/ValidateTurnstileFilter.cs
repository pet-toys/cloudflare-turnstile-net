#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace PetToys.CloudflareTurnstileNet
{
    internal sealed class ValidateTurnstileFilter(
        ITurnstileService service,
        string formField,
        string errorMessage,
        IOptionsSnapshot<CloudflareTurnstileOptions> optionsSnapshot)
        : IAsyncActionFilter, IAsyncPageFilter
    {
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

        private async Task ValidateRecaptcha(ActionContext context)
        {
            if (!optionsSnapshot.Value.Enabled) return;

            if (!context.HttpContext.Request.HasFormContentType)
            {
                context.ModelState.AddModelError(string.Empty, errorMessage);
            }
            else
            {
                if (!context.HttpContext.Request.Form.TryGetValue(formField, out var token)
                    ||
                    !await service.VerifyAsync(token.ToString()))
                {
                    context.ModelState.AddModelError(formField, errorMessage);
                }
            }
        }
    }
}
