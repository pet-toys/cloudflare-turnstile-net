using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;
using static PetToys.CloudflareTurnstileNet.Tests.TurnstileFilterHarness;

namespace PetToys.CloudflareTurnstileNet.Tests;

public sealed class ValidateTurnstileFilterTest
{
    private const string FormErrorMessage = "form-error";
    private const string FieldErrorMessage = "field-error";

    [Fact]
    public async Task Action_Disabled_SkipsValidationAndContinues()
    {
        var service = ServiceReturning(false);
        var context = ActionContext(HttpContext(enabled: false, form: FormWithToken(), service: service.Object));

        var nextCalled = await RunAsync(CreateFilter(), context);

        nextCalled.Should().BeTrue();
        context.ModelState.IsValid.Should().BeTrue();
        VerifyNeverCalled(service);
    }

    [Fact]
    public async Task Action_NonFormRequest_AddsFormErrorWithoutCallingService()
    {
        var service = ServiceReturning(true);
        var context = ActionContext(HttpContext(hasForm: false, service: service.Object));

        await RunAsync(CreateFilter(), context);

        FormErrors(context).Should().Contain(FormErrorMessage);
        VerifyNeverCalled(service);
    }

    [Fact]
    public async Task Action_MissingToken_AddsErrorsWithoutCallingService()
    {
        var service = ServiceReturning(true);
        var context = ActionContext(HttpContext(service: service.Object));

        await RunAsync(CreateFilter(), context);

        FormErrors(context).Should().Contain(FormErrorMessage);
        FieldErrors(context).Should().Contain(FieldErrorMessage);
        VerifyNeverCalled(service);
    }

    [Fact]
    public async Task Action_VerificationFails_AddsFieldErrorUnderFormFieldKey()
    {
        var service = ServiceReturning(false);
        var context = ActionContext(HttpContext(form: FormWithToken("bad"), service: service.Object));

        await RunAsync(CreateFilter(), context);

        FieldErrors(context).Should().Contain(FieldErrorMessage);
        FormErrors(context).Should().Contain(FormErrorMessage).And.NotContain(FieldErrorMessage);
    }

    [Fact]
    public async Task Action_VerificationFails_NullFieldMessage_AddsOnlyFormError()
    {
        var service = ServiceReturning(false);
        var filter = new ValidateTurnstileFilter(FormField, FormErrorMessage, fieldErrorMessage: null, useRemoteIp: false, useIdempotencyKey: false);
        var context = ActionContext(HttpContext(form: FormWithToken("bad"), service: service.Object));

        await RunAsync(filter, context);

        FormErrors(context).Should().Contain(FormErrorMessage);
        context.ModelState.ContainsKey(FormField).Should().BeFalse();
    }

    [Fact]
    public async Task Action_VerificationSucceeds_AddsNoErrors()
    {
        var service = ServiceReturning(true);
        var context = ActionContext(HttpContext(form: FormWithToken(), service: service.Object));

        var nextCalled = await RunAsync(CreateFilter(), context);

        nextCalled.Should().BeTrue();
        context.ModelState.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Action_UseRemoteIp_PassesConnectionRemoteIpToService()
    {
        var remoteIp = IPAddress.Parse("203.0.113.42");
        var service = ServiceReturning(true);
        var filter = new ValidateTurnstileFilter(FormField, FormErrorMessage, FieldErrorMessage, useRemoteIp: true, useIdempotencyKey: false);
        var context = ActionContext(HttpContext(form: FormWithToken(), remoteIp: remoteIp, service: service.Object));

        await RunAsync(filter, context);

        service.Verify(s => s.VerifyAsync(It.IsAny<string>(), remoteIp, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Action_WithoutUseRemoteIp_PassesNullRemoteIp()
    {
        var service = ServiceReturning(true);
        var filter = new ValidateTurnstileFilter(FormField, FormErrorMessage, FieldErrorMessage, useRemoteIp: false, useIdempotencyKey: false);
        var context = ActionContext(HttpContext(form: FormWithToken(), remoteIp: IPAddress.Parse("203.0.113.42"), service: service.Object));

        await RunAsync(filter, context);

        service.Verify(s => s.VerifyAsync(It.IsAny<string>(), null, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Action_UseIdempotencyKey_PassesFlagToService()
    {
        var service = ServiceReturning(true);
        var filter = new ValidateTurnstileFilter(FormField, FormErrorMessage, FieldErrorMessage, useRemoteIp: false, useIdempotencyKey: true);
        var context = ActionContext(HttpContext(form: FormWithToken(), service: service.Object));

        await RunAsync(filter, context);

        service.Verify(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Action_PassesRequestAbortedToService()
    {
        using var cts = new CancellationTokenSource();
        var service = ServiceReturning(true);
        var httpContext = HttpContext(form: FormWithToken(), service: service.Object);
        httpContext.RequestAborted = cts.Token;
        var context = ActionContext(httpContext);

        await RunAsync(CreateFilter(), context);

        service.Verify(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), It.IsAny<bool>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task Action_LocalizesErrorMessages_WhenLocalizerRegistered()
    {
        var localizer = new Mock<IStringLocalizer>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, $"localized:{name}"));
        var factory = new Mock<IStringLocalizerFactory>();
        factory.Setup(f => f.Create(It.IsAny<Type>())).Returns(localizer.Object);

        var service = ServiceReturning(false);
        var descriptor = new ControllerActionDescriptor { ControllerTypeInfo = typeof(DummyController).GetTypeInfo() };
        var context = ActionContext(HttpContext(form: FormWithToken("bad"), localizerFactory: factory.Object, service: service.Object), descriptor);

        await RunAsync(CreateFilter(), context);

        FormErrors(context).Should().Contain($"localized:{FormErrorMessage}");
        FieldErrors(context).Should().Contain($"localized:{FieldErrorMessage}");
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task Page_SafeMethod_SkipsValidationAndContinues(string method)
    {
        var service = ServiceReturning(false);
        var context = PageContext(HttpContext(form: FormWithToken(), service: service.Object), method);

        var nextCalled = await RunPageAsync(CreateFilter(), context);

        nextCalled.Should().BeTrue();
        context.ModelState.IsValid.Should().BeTrue();
        VerifyNeverCalled(service);
    }

    [Fact]
    public async Task Page_PostRequest_VerificationFails_AddsErrors()
    {
        var service = ServiceReturning(false);
        var context = PageContext(HttpContext(form: FormWithToken("bad"), service: service.Object), "POST");

        var nextCalled = await RunPageAsync(CreateFilter(), context);

        nextCalled.Should().BeTrue();
        FormErrors(context).Should().Contain(FormErrorMessage);
        service.Verify(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReusableFilter_ResolvesServicePerRequest()
    {
        // IsReusable is true, so one filter instance serves every request; it must
        // resolve ITurnstileService from each request's scope, never capture one.
        var filter = CreateReusableFilter();

        var pass = ServiceReturning(true);
        var first = ActionContext(HttpContext(form: FormWithToken(), service: pass.Object));
        await RunAsync(filter, first);

        var fail = ServiceReturning(false);
        var second = ActionContext(HttpContext(form: FormWithToken("bad"), service: fail.Object));
        await RunAsync(filter, second);

        first.ModelState.IsValid.Should().BeTrue();
        second.ModelState.IsValid.Should().BeFalse();
        pass.Verify(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        fail.Verify(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReusableFilter_ReadsOptionsPerRequest()
    {
        // The same reused filter must observe each request's own options, not a value
        // frozen at the first request.
        var filter = CreateReusableFilter();
        var service = ServiceReturning(false);

        var disabled = ActionContext(HttpContext(enabled: false, form: FormWithToken("bad"), service: service.Object));
        var nextDisabled = await RunAsync(filter, disabled);

        var enabled = ActionContext(HttpContext(enabled: true, form: FormWithToken("bad"), service: service.Object));
        await RunAsync(filter, enabled);

        nextDisabled.Should().BeTrue();
        disabled.ModelState.IsValid.Should().BeTrue();
        enabled.ModelState.IsValid.Should().BeFalse();
    }

    private static ValidateTurnstileFilter CreateFilter()
        => new(FormField, FormErrorMessage, FieldErrorMessage, useRemoteIp: false, useIdempotencyKey: false);

    private static ValidateTurnstileFilter CreateReusableFilter()
    {
        var attribute = new ValidateCloudflareTurnstileAttribute
        {
            FormErrorMessage = FormErrorMessage,
            FieldErrorMessage = FieldErrorMessage,
        };

        return (ValidateTurnstileFilter)attribute.CreateInstance(new ServiceCollection().BuildServiceProvider());
    }

    private static void VerifyNeverCalled(Mock<ITurnstileService> service)
        => service.Verify(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

    private static IEnumerable<string> FormErrors(ActionContext context)
        => context.ModelState[string.Empty]?.Errors.Select(e => e.ErrorMessage) ?? [];

    private static IEnumerable<string> FieldErrors(ActionContext context)
        => context.ModelState[FormField]?.Errors.Select(e => e.ErrorMessage) ?? [];

    private sealed class DummyController;
}
