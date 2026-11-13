using NexusOrder.Contracts.Events;

namespace NexusOrder.Write.Application.Abstractions.Outbox;

/// <summary>
/// Stages integration events in the current database transaction before committing.
/// </summary>
public interface IOutboxWriter
{
    void Enqueue<TEvent>(TEvent integrationEvent) where TEvent : class, IIntegrationEvent;
}