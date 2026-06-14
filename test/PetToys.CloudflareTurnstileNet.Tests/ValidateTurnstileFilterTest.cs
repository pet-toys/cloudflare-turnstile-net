using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace PetToys.CloudflareTurnstileNet.Tests;

public sealed class ValidateTurnstileFilterTest
{
    private const string FormField = "cf-turnstile-response";
    private const string FormErrorMessage = "form-error";
    private const string FieldErrorMessage = "field-error";

    [Fact]
    public async Task OnActionExecution_VerificationFails_AddsFieldErrorUnderFormFieldKey()
    {
        var service = new Mock<ITurnstileService>();
        service
            .Setup(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var filter = new ValidateTurnstileFilter(service.Object, FormField, FormErrorMessage, FieldErrorMessage, false, false);
        var context = CreateActionExecutingContext(token: "bad-token");

        await filter.OnActionExecutionAsync(context, () => Task.FromResult(
            new ActionExecutedContext(context, context.Filters, controller: null!)));

        context.ModelState[FormField]!.Errors.Select(e => e.ErrorMessage).Should().Contain(FieldErrorMessage);
        context.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage).Should().Contain(FormErrorMessage);
        context.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage).Should().NotContain(FieldErrorMessage);
    }

    [Fact]
    public async Task OnActionExecution_VerificationSucceeds_AddsNoErrors()
    {
        var service = new Mock<ITurnstileService>();
        service
            .Setup(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var filter = new ValidateTurnstileFilter(service.Object, FormField, FormErrorMessage, FieldErrorMessage, false, false);
        var context = CreateActionExecutingContext(token: "good-token");

        await filter.OnActionExecutionAsync(context, () => Task.FromResult(
            new ActionExecutedContext(context, context.Filters, controller: null!)));

        context.ModelState.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task OnActionExecution_PassesRequestAbortedToService()
    {
        using var cts = new CancellationTokenSource();
        var service = new Mock<ITurnstileService>();
        service
            .Setup(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var filter = new ValidateTurnstileFilter(service.Object, FormField, FormErrorMessage, FieldErrorMessage, false, false);
        var context = CreateActionExecutingContext(token: "good-token");
        context.HttpContext.RequestAborted = cts.Token;

        await filter.OnActionExecutionAsync(context, () => Task.FromResult(
            new ActionExecutedContext(context, context.Filters, controller: null!)));

        service.Verify(
            s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), It.IsAny<bool>(), cts.Token),
            Times.Once);
    }

    private static ActionExecutingContext CreateActionExecutingContext(string token)
    {
        var services = new ServiceCollection();
        services.Configure<CloudflareTurnstileOptions>(opt => opt.Enabled = true);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            [FormField] = token,
        });

        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: null!);
    }
}
