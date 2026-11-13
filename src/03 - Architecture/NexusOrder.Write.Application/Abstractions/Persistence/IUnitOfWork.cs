using NexusOrder.Contracts.Common;

namespace NexusOrder.Write.Application.Abstractions.Persistence;

/// <summary>
/// Commits domain changes and staged Outbox messages atomically.
/// Automatically translates optimistic concurrency exceptions into Result.Failure(Error.Conflict).
/// </summary>
public interface IUnitOfWork
{
    Task<Result> CommitAsync(CancellationToken cancellationToken = default);
}