namespace NexusOrder.Contracts.Events;

/// <summary>
/// Base contract for all integration events published through the message broker.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    Guid AggregateId { get; }
    long AggregateVersion { get; }
    DateTimeOffset OccurredOnUtc { get; }
}

public sealed record OrderItemEventDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

public sealed record OrderSubmittedIntegrationEvent(
    Guid EventId,
    Guid AggregateId,
    string CustomerId,
    decimal TotalAmount,
    string Status,
    long AggregateVersion,
    DateTimeOffset OccurredOnUtc,
    IReadOnlyList<OrderItemEventDto> Items) : IIntegrationEvent;

public sealed record OrderPaidIntegrationEvent(
    Guid EventId,
    Guid AggregateId,
    string PaymentTransactionId,
    string Status,
    long AggregateVersion,
    DateTimeOffset OccurredOnUtc) : IIntegrationEvent;

public sealed record OrderCancelledIntegrationEvent(
    Guid EventId,
    Guid AggregateId,
    string CancellationReason,
    string Status,
    long AggregateVersion,
    DateTimeOffset OccurredOnUtc) : IIntegrationEvent;

/// <summary>
/// Emitted by the Read Projector once MongoDB has successfully applied the projection.
/// Consumed by the API to push real-time notifications via SignalR.
/// </summary>
public sealed record OrderProjectionSynchronizedEvent(
    Guid EventId,
    Guid AggregateId,
    string CustomerId,
    string CurrentStatus,
    long AggregateVersion,
    DateTimeOffset OccurredOnUtc) : IIntegrationEvent;