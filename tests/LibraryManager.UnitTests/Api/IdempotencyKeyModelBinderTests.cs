using System.Globalization;
using LibraryManager.Api.Contracts.Common;
using LibraryManager.Api.ModelBinding;
using LibraryManager.Api.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Primitives;

namespace LibraryManager.UnitTests.Api;

public sealed class IdempotencyKeyModelBinderTests
{
    [Fact]
    public async Task Missing_header_adds_required_model_state_error_without_throwing()
    {
        var (binder, bindingContext) = CreateBinder();

        await binder.BindModelAsync(bindingContext);

        AssertBindingFailed(bindingContext, "Validation_IdempotencyKey_Required");
    }

    [Fact]
    public async Task Empty_header_adds_required_model_state_error_without_throwing()
    {
        var (binder, bindingContext) = CreateBinder(headerValue: string.Empty);

        await binder.BindModelAsync(bindingContext);

        AssertBindingFailed(bindingContext, "Validation_IdempotencyKey_Required");
    }

    [Fact]
    public async Task Whitespace_header_adds_required_model_state_error_without_throwing()
    {
        var (binder, bindingContext) = CreateBinder(headerValue: " \t ");

        await binder.BindModelAsync(bindingContext);

        AssertBindingFailed(bindingContext, "Validation_IdempotencyKey_Required");
    }

    [Fact]
    public async Task Header_longer_than_max_length_adds_max_length_error_without_throwing()
    {
        var (binder, bindingContext) = CreateBinder(headerValue: new string('a', IdempotencyKey.MaxLength + 1));

        await binder.BindModelAsync(bindingContext);

        AssertBindingFailed(bindingContext, "Validation_IdempotencyKey_MaxLength");
    }

    [Fact]
    public async Task Valid_header_is_trimmed_and_bound()
    {
        var (binder, bindingContext) = CreateBinder(headerValue: "  loan-key-1  ");

        await binder.BindModelAsync(bindingContext);

        Assert.True(bindingContext.Result.IsModelSet);
        var key = Assert.IsType<IdempotencyKey>(bindingContext.Result.Model);
        Assert.Equal("loan-key-1", key.Value);
        Assert.True(bindingContext.ModelState.IsValid);
    }

    [Fact]
    public async Task Header_of_max_length_is_bound()
    {
        var raw = new string('b', IdempotencyKey.MaxLength);
        var (binder, bindingContext) = CreateBinder(headerValue: raw);

        await binder.BindModelAsync(bindingContext);

        Assert.True(bindingContext.Result.IsModelSet);
        var key = Assert.IsType<IdempotencyKey>(bindingContext.Result.Model);
        Assert.Equal(raw, key.Value);
    }

    [Fact]
    public async Task Canceled_request_is_not_swallowed()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var (binder, bindingContext) = CreateBinder(requestAborted: canceled.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => binder.BindModelAsync(bindingContext));
    }

    [Fact]
    public async Task OperationCanceledException_from_localizer_is_not_swallowed()
    {
        var (binder, bindingContext) = CreateBinder(new CancelingLocalizer());

        await Assert.ThrowsAsync<OperationCanceledException>(() => binder.BindModelAsync(bindingContext));
    }

    private static (IdempotencyKeyModelBinder Binder, ModelBindingContext BindingContext) CreateBinder(
        IStringLocalizer<SharedResource>? localizer = null,
        string? headerValue = null,
        CancellationToken requestAborted = default)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestAborted = requestAborted
        };

        if (headerValue is not null)
        {
            httpContext.Request.Headers[IdempotencyKey.HeaderName] = new StringValues(headerValue);
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(IdempotencyKey));
        var bindingContext = DefaultModelBindingContext.CreateBindingContext(
            actionContext,
            new QueryStringValueProvider(
                BindingSource.Query,
                httpContext.Request.Query,
                CultureInfo.InvariantCulture),
            metadata,
            bindingInfo: null,
            modelName: "idempotencyKey");

        return (new IdempotencyKeyModelBinder(localizer ?? new StaticLocalizer()), bindingContext);
    }

    private static void AssertBindingFailed(ModelBindingContext bindingContext, string modelStateKey)
    {
        Assert.False(bindingContext.Result.IsModelSet);
        Assert.False(bindingContext.ModelState.IsValid);
        Assert.True(bindingContext.ModelState.TryGetValue(modelStateKey, out var entry));
        Assert.NotNull(entry);
        Assert.NotEmpty(entry.Errors);
    }

    private sealed class StaticLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class CancelingLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => throw new OperationCanceledException();

        public LocalizedString this[string name, params object[] arguments] => throw new OperationCanceledException();

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            throw new OperationCanceledException();
    }
}
