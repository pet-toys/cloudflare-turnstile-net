using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Primitives;
using Moq;

namespace PetToys.CloudflareTurnstileNet.Tests;

/// <summary>
/// Builders that assemble the ASP.NET filter pipeline plumbing required to
/// drive <see cref="ValidateTurnstileFilter"/> from a unit test.
/// </summary>
internal static class TurnstileFilterHarness
{
    public const string FormField = "cf-turnstile-response";

    public static Mock<ITurnstileService> ServiceReturning(bool result)
    {
        var mock = new Mock<ITurnstileService>();
        mock
            .Setup(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mock;
    }

    public static DefaultHttpContext HttpContext(
        bool enabled = true,
        bool hasForm = true,
        IDictionary<string, StringValues>? form = null,
        IPAddress? remoteIp = null,
        IStringLocalizerFactory? localizerFactory = null,
        ITurnstileService? service = null)
    {
        var services = new ServiceCollection();
        services.Configure<CloudflareTurnstileOptions>(opt => opt.Enabled = enabled);
        if (service is not null)
        {
            services.AddSingleton(service);
        }

        if (localizerFactory is not null)
        {
            services.AddSingleton(localizerFactory);
        }

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };

        if (remoteIp is not null)
        {
            httpContext.Connection.RemoteIpAddress = remoteIp;
        }

        if (hasForm)
        {
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";
            httpContext.Request.Form = new FormCollection(
                new Dictionary<string, StringValues>(form ?? new Dictionary<string, StringValues>()));
        }

        return httpContext;
    }

    public static IDictionary<string, StringValues> FormWithToken(string token = "token")
        => new Dictionary<string, StringValues> { [FormField] = token };

    public static ActionExecutingContext ActionContext(HttpContext httpContext, ActionDescriptor? descriptor = null, string method = "POST")
    {
        httpContext.Request.Method = method;

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            descriptor ?? new ActionDescriptor(),
            new ModelStateDictionary());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: null!);
    }

    public static PageHandlerExecutingContext PageContext(HttpContext httpContext, string method)
    {
        httpContext.Request.Method = method;

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new CompiledPageActionDescriptor(),
            new ModelStateDictionary());

        return new PageHandlerExecutingContext(
            new PageContext(actionContext),
            new List<IFilterMetadata>(),
            handlerMethod: null!,
            new Dictionary<string, object?>(),
            handlerInstance: new object());
    }

    public static async Task<bool> RunAsync(ValidateTurnstileFilter filter, ActionExecutingContext context)
    {
        var nextCalled = false;
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, context.Filters, controller: null!));
        });
        return nextCalled;
    }

    public static async Task<bool> RunPageAsync(ValidateTurnstileFilter filter, PageHandlerExecutingContext context)
    {
        var nextCalled = false;
        await filter.OnPageHandlerExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new PageHandlerExecutedContext(new PageContext(context), context.Filters, context.HandlerMethod!, context.HandlerInstance!));
        });
        return nextCalled;
    }
}
