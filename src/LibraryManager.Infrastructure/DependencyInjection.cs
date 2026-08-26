using LibraryManager.Application.Abstractions;
using LibraryManager.Infrastructure.Outbox;
using LibraryManager.Infrastructure.Persistence;
using LibraryManager.Infrastructure.Persistence.Repositories;
using LibraryManager.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
