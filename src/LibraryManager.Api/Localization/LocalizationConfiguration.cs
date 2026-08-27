using System.Globalization;
using LibraryManager.Api.Resources;
using LibraryManager.Application.Abstractions;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace LibraryManager.Api.Localization;

public static class LocalizationConfiguration
{
    public const string DefaultCulture = "en-US";

    public static readonly CultureInfo[] SupportedCultures =
    [
        new(DefaultCulture),
        new("pt-BR")
    ];

    public static IServiceCollection AddLibraryManagerLocalization(this IServiceCollection services)
    {
        services.AddLocalization();
        services.AddSingleton<ErrorLocalizer>();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(DefaultCulture);
            options.SupportedCultures = SupportedCultures;
            options.SupportedUICultures = SupportedCultures;
            options.RequestCultureProviders =
            [
                new AcceptLanguageHeaderRequestCultureProvider()
            ];
            options.ApplyCurrentCultureToResponseHeaders = true;
        });

        return services;
    }

    public static IMvcBuilder AddLibraryManagerDataAnnotationsLocalization(this IMvcBuilder mvc)
    {
        return mvc
            .AddDataAnnotationsLocalization(options =>
            {
                options.DataAnnotationLocalizerProvider = (_, factory) =>
                    factory.Create(typeof(SharedResource));
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var localizer = context.HttpContext.RequestServices
                        .GetRequiredService<IStringLocalizer<SharedResource>>();
                    var correlationId = context.HttpContext.RequestServices
                        .GetService<ICorrelationContext>()?.CorrelationId;

                    var problem = new ValidationProblemDetails(context.ModelState)
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = localizer["Problem_Validation_Title"],
                        Instance = context.HttpContext.Request.Path
                    };
                    problem.Extensions["correlationId"] = correlationId;

                    return new BadRequestObjectResult(problem)
                    {
                        ContentTypes = { "application/problem+json" }
                    };
                };
            });
    }

    public static IApplicationBuilder UseLibraryManagerRequestLocalization(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<RequestLocalizationOptions>>();
        return app.UseRequestLocalization(options.Value);
    }
}
