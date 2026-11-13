using FluentValidation;
using NexusOrder.Contracts.Common;
using NexusOrder.Contracts.Events;
using NexusOrder.Write.Application.Abstractions.Messaging;
using NexusOrder.Write.Application.Abstractions.Outbox;
using NexusOrder.Write.Application.Abstractions.Persistence;

namespace NexusOrder.Write.Application.Orders.PayOrder;

public sealed record PayOrderCommand(Guid OrderId, string PaymentTransactionId) : ICommand;

public sealed class PayOrderCommandValidator : AbstractValidator<PayOrderCommand>
{
    public PayOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.PaymentTransactionId).NotEmpty().MaximumLength(150);
    }
}

public sealed class PayOrderCommandHandler(
    IValidator<PayOrderCommand> validator,
    IOrderRepository orderRepository,
    IOutboxWriter outboxWriter,
    IUnitOfWork unitOfWork) : ICommandHandler<PayOrderCommand>
{
    public async Task<Result> HandleAsync(PayOrderCommand command, CancellationToken cancellationToken = default)
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

        var payResult = order.MarkAsPaid(command.PaymentTransactionId);
        if (payResult.IsFailure)
        {
            return payResult;
        }

        outboxWriter.Enqueue(new OrderPaidIntegrationEvent(
            EventId: Guid.CreateVersion7(),
            AggregateId: order.Id,
            PaymentTransactionId: order.PaymentTransactionId!,
            Status: order.Status.ToString(),
            AggregateVersion: order.Version,
            OccurredOnUtc: order.UpdatedAtUtc ?? DateTimeOffset.UtcNow));

        return await unitOfWork.CommitAsync(cancellationToken);
    }
}