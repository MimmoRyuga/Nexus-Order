using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusOrder.Write.Application.Abstractions.Outbox;
using NexusOrder.Write.Application.Abstractions.Persistence;
using NexusOrder.Write.Infrastructure.Outbox;
using NexusOrder.Write.Infrastructure.Persistence;

namespace NexusOrder.Write.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWriteInfrastructure(
        this IServiceCollection services,
        string postgresConnectionString)
    {
        services.AddDbContext<WriteDbContext>(options =>
            options.UseNpgsql(postgresConnectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOutboxWriter>(sp => sp.GetRequiredService<WriteDbContext>());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<WriteDbContext>());

        // Register background Outbox publisher
        services.AddHostedService<OutboxBackgroundProcessor>();

        return services;
    }
}