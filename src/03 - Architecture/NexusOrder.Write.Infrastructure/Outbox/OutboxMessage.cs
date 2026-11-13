namespace NexusOrder.Write.Infrastructure.Outbox;

/// <summary>
/// Represents an integration event persisted atomically within the Write DB transaction.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public Guid AggregateId { get; init; }
    public long AggregateVersion { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public DateTimeOffset OccurredOnUtc { get; init; }
    public DateTimeOffset? ProcessedOnUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}