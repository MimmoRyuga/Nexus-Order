using FluentValidation;
using NexusOrder.Contracts.Common;
using NexusOrder.Contracts.Events;
using NexusOrder.Write.Application.Abstractions.Messaging;
using NexusOrder.Write.Application.Abstractions.Outbox;
using NexusOrder.Write.Application.Abstractions.Persistence;

namespace NexusOrder.Write.Application.Orders.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId, string Reason) : ICommand;

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class CancelOrderCommandHandler(
    IValidator<CancelOrderCommand> validator,
    IOrderRepository orderRepository,
    IOutboxWriter outboxWriter,
    IUnitOfWork unitOfWork) : ICommandHandler<CancelOrderCommand>
{
    public async Task<Result> HandleAsync(CancelOrderCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errorMessage = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Failure(Error.Validation(errorMessage));
        }

        var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure(Error.NotFound($"Order with ID '{command.OrderId}' was not found."));
        }

        var cancelResult = order.Cancel(command.Reason);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        outboxWriter.Enqueue(new OrderCancelledIntegrationEvent(
            EventId: Guid.CreateVersion7(),
            AggregateId: order.Id,
            CancellationReason: order.CancellationReason!,
            Status: order.Status.ToString(),
            AggregateVersion: order.Version,
            OccurredOnUtc: order.UpdatedAtUtc ?? DateTimeOffset.UtcNow));

        return await unitOfWork.CommitAsync(cancellationToken);
    }
}