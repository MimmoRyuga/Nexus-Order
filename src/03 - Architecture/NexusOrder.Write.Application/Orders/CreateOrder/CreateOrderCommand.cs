using FluentValidation;
using NexusOrder.Contracts.Common;
using NexusOrder.Contracts.Events;
using NexusOrder.Write.Application.Abstractions.Messaging;
using NexusOrder.Write.Application.Abstractions.Outbox;
using NexusOrder.Write.Application.Abstractions.Persistence;
using NexusOrder.Write.Domain.Orders;

namespace NexusOrder.Write.Application.Orders.CreateOrder;

public sealed record CreateOrderItemInput(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

public sealed record CreateOrderCommand(
    string CustomerId,
    IReadOnlyList<CreateOrderItemInput> Items) : ICommand<Guid>;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one order item is required.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.ProductName).NotEmpty().MaximumLength(200);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThan(0);
        });
    }
}

public sealed class CreateOrderCommandHandler(
    IValidator<CreateOrderCommand> validator,
    IOrderRepository orderRepository,
    IOutboxWriter outboxWriter,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateOrderCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errorMessage = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<Guid>(Error.Validation(errorMessage));
        }

        var orderResult = Order.Create(
            command.CustomerId,
            command.Items.Select(i => (i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)));

        if (orderResult.IsFailure)
        {
            return Result.Failure<Guid>(orderResult.Error);
        }

        var order = orderResult.Value;
        await orderRepository.AddAsync(order, cancellationToken);

        // Stage integration event in the same database transaction
        outboxWriter.Enqueue(new OrderSubmittedIntegrationEvent(
            EventId: Guid.CreateVersion7(),
            AggregateId: order.Id,
            CustomerId: order.CustomerId,
            TotalAmount: order.TotalAmount,
            Status: order.Status.ToString(),
            AggregateVersion: order.Version,
            OccurredOnUtc: order.CreatedAtUtc,
            Items: order.Items
                .Select(i => new OrderItemEventDto(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice))
                .ToList()));

        var commitResult = await unitOfWork.CommitAsync(cancellationToken);
        if (commitResult.IsFailure)
        {
            return Result.Failure<Guid>(commitResult.Error);
        }

        return Result.Success(order.Id);
    }
}