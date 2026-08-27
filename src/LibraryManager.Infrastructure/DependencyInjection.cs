using LibraryManager.Application.Abstractions;
using LibraryManager.Infrastructure.Caching;
using LibraryManager.Infrastructure.Idempotency;
using LibraryManager.Infrastructure.Outbox;
using LibraryManager.Infrastructure.Persistence;
using LibraryManager.Infrastructure.Persistence.Repositories;
using LibraryManager.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibraryManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLibraryManagerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var postgres = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=library_manager;Username=postgres;Password=postgres";

        services.AddDbContext<LibraryDbContext>(options => options.UseNpgsql(postgres));
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<OutboxClaimer>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddSingleton<RedisAvailabilityCache>();
        services.AddKeyedSingleton<IAvailabilityCache>(
            RedisAvailabilityCache.ServiceKey,
            (sp, _) => sp.GetRequiredService<RedisAvailabilityCache>());
        services.AddSingleton<IAvailabilityCache>(sp =>
            new ResilientAvailabilityCacheDecorator(
                sp.GetRequiredService<RedisAvailabilityCache>(),
                sp.GetRequiredService<ILogger<ResilientAvailabilityCacheDecorator>>(),
                sp.GetRequiredService<ILibraryManagerMetrics>()));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<OutboxProcessor>();
        if (!string.Equals(configuration["Outbox:ProcessorEnabled"], "false", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHostedService(sp => sp.GetRequiredService<OutboxProcessor>());
        }

        return services;
    }
}
