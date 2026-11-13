using NexusOrder.Contracts.Common;

namespace NexusOrder.Write.Domain.Orders;

/// <summary>
/// Order Aggregate Root. Protects transactional business invariants on the Write Side
/// and tracks aggregate version for out-of-order protection on the Read Side.
/// </summary>
public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? PaymentTransactionId { get; private set; }
    public string? CancellationReason { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { } // Required by EF Core

    public static Result<Order> Create(
        string customerId,
        IEnumerable<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)> items)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Result.Failure<Order>(Error.DomainRule("CustomerId cannot be empty."));
        }

        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            return Result.Failure<Order>(Error.DomainRule("An order must contain at least one item."));
        }

        var order = new Order
        {
            Id = Guid.CreateVersion7(), // Time-ordered UUIDv7 for optimal PostgreSQL B-Tree indexing
            CustomerId = customerId.Trim(),
            Status = OrderStatus.Submitted,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Version = 1
        };

        // Merge duplicate products if any
        foreach (var group in itemList.GroupBy(x => x.ProductId))
        {
            var first = group.First();
            var totalQuantity = group.Sum(x => x.Quantity);

            if (totalQuantity <= 0)
            {
                return Result.Failure<Order>(Error.DomainRule($"Quantity for product '{first.ProductName}' must be greater than zero."));
            }

            if (first.UnitPrice <= 0)
            {
                return Result.Failure<Order>(Error.DomainRule($"Unit price for product '{first.ProductName}' must be greater than zero."));
            }

            order._items.Add(new OrderItem(
                order.Id,
                first.ProductId,
                first.ProductName.Trim(),
                totalQuantity,
                first.UnitPrice));
        }

        order.TotalAmount = order._items.Sum(i => i.Quantity * i.UnitPrice);
        return Result.Success(order);
    }

    public Result MarkAsPaid(string paymentTransactionId)
    {
        if (string.IsNullOrWhiteSpace(paymentTransactionId))
        {
            return Result.Failure(Error.DomainRule("Payment transaction ID is required."));
        }

        if (Status == OrderStatus.Paid)
        {
            return Result.Failure(Error.DomainRule("Order is already paid."));
        }

        if (Status == OrderStatus.Cancelled)
        {
            return Result.Failure(Error.DomainRule("Cannot pay a cancelled order."));
        }

        Status = OrderStatus.Paid;
        PaymentTransactionId = paymentTransactionId.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.DomainRule("Cancellation reason is required."));
        }

        if (Status == OrderStatus.Paid)
        {
            return Result.Failure(Error.DomainRule("Cannot cancel an order that has already been paid."));
        }

        if (Status == OrderStatus.Cancelled)
        {
            return Result.Failure(Error.DomainRule("Order is already cancelled."));
        }

        Status = OrderStatus.Cancelled;
        CancellationReason = reason.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;

        return Result.Success();
    }
}