using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NexusOrder.Write.Application.Abstractions.Messaging;
using NexusOrder.Write.Application.Orders.CancelOrder;
using NexusOrder.Write.Application.Orders.CreateOrder;
using NexusOrder.Write.Application.Orders.PayOrder;

namespace NexusOrder.Write.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddWriteApplication(this IServiceCollection services)
    {
        // Register FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Register native Command Handlers (No MediatR required)
        services.AddScoped<ICommandHandler<CreateOrderCommand, Guid>, CreateOrderCommandHandler>();
        services.AddScoped<ICommandHandler<PayOrderCommand>, PayOrderCommandHandler>();
        services.AddScoped<ICommandHandler<CancelOrderCommand>, CancelOrderCommandHandler>();

        return services;
    }
}