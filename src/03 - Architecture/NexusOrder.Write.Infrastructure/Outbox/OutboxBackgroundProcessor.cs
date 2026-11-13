using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexusOrder.Write.Infrastructure.Persistence;
using RabbitMQ.Client;
using System.Text;
using System.Threading.Channels;

namespace NexusOrder.Write.Infrastructure.Outbox;

/// <summary>
/// Polls the PostgreSQL outbox table using row-level locking (FOR UPDATE SKIP LOCKED)
/// and publishes events reliably to RabbitMQ using Publisher Confirms.
/// </summary>
public sealed class OutboxBackgroundProcessor(
    IServiceScopeFactory scopeFactory,
    IConnection rabbitConnection,
    ILogger<OutboxBackgroundProcessor> logger) : BackgroundService
{
    public const string ExchangeName = "nexusorder.events.exchange";
    private const int BatchSize = 50;
    private const int MaxRetries = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize RabbitMQ Topic Exchange using RabbitMQ.Client v7 async API
        await using var channel = await rabbitConnection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            stoppingToken);

        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessPendingBatchAsync(channel, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unexpected error while processing Outbox batch.");
            }
        }
    }

    private async Task ProcessPendingBatchAsync(IChannel channel, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        // Lock batch exclusively; other scaled instances will skip locked rows automatically
        var messages = await dbContext.OutboxMessages
            .FromSqlRaw("""
                SELECT * FROM outbox_messages
                WHERE "ProcessedOnUtc" IS NULL AND "RetryCount" < {0}
                ORDER BY "OccurredOnUtc" ASC
                LIMIT {1}
                FOR UPDATE SKIP LOCKED
                """, MaxRetries, BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(message.PayloadJson);
                var properties = new BasicProperties
                {
                    MessageId = message.Id.ToString(),
                    CorrelationId = message.AggregateId.ToString(),
                    Type = message.EventType,
                    DeliveryMode = DeliveryModes.Persistent,
                    ContentType = "application/json",
                    Timestamp = new AmqpTimestamp(message.OccurredOnUtc.ToUnixTimeSeconds())
                };

                // Publish with broker acknowledgement (Publisher Confirms enabled on channel)
                await channel.BasicPublishAsync(
                    exchange: ExchangeName,
                    routingKey: message.EventType,
                    mandatory: true,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: ct);

                message.ProcessedOnUtc = DateTimeOffset.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.LastError = ex.Message;
                logger.LogWarning(ex,
                    "Failed to publish OutboxMessage {MessageId} (Attempt {Retry}/{MaxRetries}).",
                    message.Id, message.RetryCount, MaxRetries);
            }
        }

        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}