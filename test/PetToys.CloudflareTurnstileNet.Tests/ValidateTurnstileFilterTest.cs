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
        var context = ActionContext(HttpContext(enabled: false, form: FormWithToken()));

        var nextCalled = await RunAsync(CreateFilter(service), context);

        nextCalled.Should().BeTrue();
        context.ModelState.IsValid.Should().BeTrue();
        VerifyNeverCalled(service);
    }

    [Fact]
    public async Task Action_NonFormRequest_AddsFormErrorWithoutCallingService()
    {
        var service = ServiceReturning(true);
        var context = ActionContext(HttpContext(hasForm: false));

        await RunAsync(CreateFilter(service), context);

        FormErrors(context).Should().Contain(FormErrorMessage);
        VerifyNeverCalled(service);
    }

    [Fact]
    public async Task Action_MissingToken_AddsErrorsWithoutCallingService()
    {
        var service = ServiceReturning(true);
        var context = ActionContext(HttpContext());

        await RunAsync(CreateFilter(service), context);

        FormErrors(context).Should().Contain(FormErrorMessage);
        FieldErrors(context).Should().Contain(FieldErrorMessage);
        VerifyNeverCalled(service);
    }

    [Fact]
    public async Task Action_VerificationFails_AddsFieldErrorUnderFormFieldKey()
    {
        var service = ServiceReturning(false);
        var context = ActionContext(HttpContext(form: FormWithToken("bad")));

        await RunAsync(CreateFilter(service), context);

        FieldErrors(context).Should().Contain(FieldErrorMessage);
        FormErrors(context).Should().Contain(FormErrorMessage).And.NotContain(FieldErrorMessage);
    }

    [Fact]
    public async Task Action_VerificationFails_NullFieldMessage_AddsOnlyFormError()
    {
        var service = ServiceReturning(false);
        var filter = new ValidateTurnstileFilter(service.Object, FormField, FormErrorMessage, fieldErrorMessage: null, useRemoteIp: false, useIdempotencyKey: false);
        var context = ActionContext(HttpContext(form: FormWithToken("bad")));

        await RunAsync(filter, context);

        FormErrors(context).Should().Contain(FormErrorMessage);
        context.ModelState.ContainsKey(FormField).Should().BeFalse();
    }

    [Fact]
    public async Task Action_VerificationSucceeds_AddsNoErrors()
    {
        var service = ServiceReturning(true);
        var context = ActionContext(HttpContext(form: FormWithToken()));

        var nextCalled = await RunAsync(CreateFilter(service), context);

        nextCalled.Should().BeTrue();
        context.ModelState.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Action_UseRemoteIp_PassesConnectionRemoteIpToService()
    {
        var remoteIp = IPAddress.Parse("203.0.113.42");
        var service = ServiceReturning(true);
        var filter = new ValidateTurnstileFilter(service.Object, FormField, FormErrorMessage, FieldErrorMessage, useRemoteIp: true, useIdempotencyKey: false);
        var context = ActionContext(HttpContext(form: FormWithToken(), remoteIp: remoteIp));

        await RunAsync(filter, context);

        service.Verify(s => s.VerifyAsync(It.IsAny<string>(), remoteIp, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Action_WithoutUseRemoteIp_PassesNullRemoteIp()
    {
        var service = ServiceReturning(true);
        var filter = new ValidateTurnstileFilter(service.Object, FormField, FormErrorMessage, FieldErrorMessage, useRemoteIp: false, useIdempotencyKey: false);
        var context = ActionContext(HttpContext(form: FormWithToken(), remoteIp: IPAddress.Parse("203.0.113.42")));

        await RunAsync(filter, context);

        service.Verify(s => s.VerifyAsync(It.IsAny<string>(), null, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Action_UseIdempotencyKey_PassesFlagToService()
    {
        var service = ServiceReturning(true);
        var filter = new ValidateTurnstileFilter(service.Object, FormField, FormErrorMessage, FieldErrorMessage, useRemoteIp: false, useIdempotencyKey: true);
        var context = ActionContext(HttpContext(form: FormWithToken()));

        await RunAsync(filter, context);

        service.Verify(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Action_PassesRequestAbortedToService()
    {
        using var cts = new CancellationTokenSource();
        var service = ServiceReturning(true);
        var httpContext = HttpContext(form: FormWithToken());
        httpContext.RequestAborted = cts.Token;
        var context = ActionContext(httpContext);

        await RunAsync(CreateFilter(service), context);

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
        var context = ActionContext(HttpContext(form: FormWithToken("bad"), localizerFactory: factory.Object), descriptor);

        await RunAsync(CreateFilter(service), context);

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
        var context = PageContext(HttpContext(form: FormWithToken()), method);

        var nextCalled = await RunPageAsync(CreateFilter(service), context);

        nextCalled.Should().BeTrue();
        context.ModelState.IsValid.Should().BeTrue();
        VerifyNeverCalled(service);
    }

    [Fact]
    public async Task Page_PostRequest_VerificationFails_AddsErrors()
    {
        var service = ServiceReturning(false);
        var context = PageContext(HttpContext(form: FormWithToken("bad")), "POST");

        var nextCalled = await RunPageAsync(CreateFilter(service), context);

        nextCalled.Should().BeTrue();
        FormErrors(context).Should().Contain(FormErrorMessage);
        service.Verify(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ValidateTurnstileFilter CreateFilter(Mock<ITurnstileService> service)
        => new(service.Object, FormField, FormErrorMessage, FieldErrorMessage, useRemoteIp: false, useIdempotencyKey: false);

    private static void VerifyNeverCalled(Mock<ITurnstileService> service)
        => service.Verify(s => s.VerifyAsync(It.IsAny<string>(), It.IsAny<IPAddress?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

    private static IEnumerable<string> FormErrors(ActionContext context)
        => context.ModelState[string.Empty]?.Errors.Select(e => e.ErrorMessage) ?? [];

    private static IEnumerable<string> FieldErrors(ActionContext context)
        => context.ModelState[FormField]?.Errors.Select(e => e.ErrorMessage) ?? [];

    private sealed class DummyController;
}
