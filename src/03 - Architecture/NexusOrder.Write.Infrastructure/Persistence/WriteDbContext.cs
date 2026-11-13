using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexusOrder.Contracts.Common;
using NexusOrder.Contracts.Events;
using NexusOrder.Write.Application.Abstractions.Outbox;
using NexusOrder.Write.Application.Abstractions.Persistence;
using NexusOrder.Write.Domain.Orders;
using NexusOrder.Write.Infrastructure.Outbox;

namespace NexusOrder.Write.Infrastructure.Persistence;

public sealed class WriteDbContext(DbContextOptions<WriteDbContext> options)
    : DbContext(options), IOutboxWriter, IUnitOfWork
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public void Enqueue<TEvent>(TEvent integrationEvent) where TEvent : class, IIntegrationEvent
    {
        var outboxMessage = new OutboxMessage
        {
            Id = integrationEvent.EventId,
            AggregateId = integrationEvent.AggregateId,
            AggregateVersion = integrationEvent.AggregateVersion,
            EventType = typeof(TEvent).Name,
            PayloadJson = JsonSerializer.Serialize(integrationEvent),
            OccurredOnUtc = integrationEvent.OccurredOnUtc
        };

        OutboxMessages.Add(outboxMessage);
    }

    public async Task<Result> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Triggered when PostgreSQL xmin row version detects a concurrent modification
            return Result.Failure(Error.Conflict(
                "The order was modified by another concurrent transaction. Please reload and retry."));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(builder =>
        {
            builder.ToTable("orders");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.CustomerId).HasMaxLength(100).IsRequired();
            builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);
            builder.Property(o => o.TotalAmount).HasPrecision(18, 2);
            builder.Property(o => o.PaymentTransactionId).HasMaxLength(150);
            builder.Property(o => o.CancellationReason).HasMaxLength(500);

            // PostgreSQL native system column for optimistic concurrency
            builder.Property<uint>("xmin").IsRowVersion();

            builder.OwnsMany(o => o.Items, itemBuilder =>
            {
                itemBuilder.ToTable("order_items");
                itemBuilder.WithOwner().HasForeignKey(i => i.OrderId);
                itemBuilder.HasKey(i => new { i.OrderId, i.ProductId });
                itemBuilder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
                itemBuilder.Property(i => i.UnitPrice).HasPrecision(18, 2);
            });

            builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(m => m.Id);

            builder.Property(m => m.EventType).HasMaxLength(250).IsRequired();
            builder.Property(m => m.PayloadJson).HasColumnType("jsonb").IsRequired();

            builder.HasIndex(m => m.OccurredOnUtc)
                .HasFilter("\"ProcessedOnUtc\" IS NULL");
        });
    }
}