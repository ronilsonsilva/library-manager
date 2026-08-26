using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Telemetry;
using LibraryManager.Infrastructure.Caching;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LibraryManager.Api.Telemetry;

public static class OpenTelemetryConfiguration
{
    public static WebApplicationBuilder AddLibraryManagerTelemetry(this WebApplicationBuilder builder)
    {
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });

        builder.Services.AddSingleton<ILibraryManagerMetrics, LibraryManagerMetrics>();

        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: "library-manager",
                serviceVersion: typeof(OpenTelemetryConfiguration).Assembly.GetName().Version?.ToString()));

        otel.WithTracing(tracing =>
        {
            tracing
                .AddSource(LibraryManagerInstrumentation.ActivitySourceName)
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.EnrichWithHttpRequest = (activity, request) =>
                    {
                        if (request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
                        {
                            activity.SetTag("correlation.id", correlationId.ToString());
                        }
                    };
                })
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddRedisInstrumentation()
                .ConfigureRedisInstrumentation((services, instrumentation) =>
                {
                    if (services.GetService<IAvailabilityCache>() is RedisAvailabilityCache cache)
                    {
                        cache.AttachInstrumentation(connection => instrumentation.AddConnection(connection));
                    }
                });

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
            }
        });

        otel.WithMetrics(metrics =>
        {
            metrics
                .AddMeter(LibraryManagerMetrics.MeterName)
                .AddAspNetCoreInstrumentation();

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
            }
        });

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeScopes = true;
                logging.IncludeFormattedMessage = true;
                logging.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
            });
        }

        return builder;
    }
}
