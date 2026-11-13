namespace NexusOrder.Write.Domain.Orders;

/// <summary>
/// Value object representing an individual line item within an Order aggregate.
/// </summary>
public sealed class OrderItem
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    private OrderItem() { } // Required by EF Core

    internal OrderItem(Guid orderId, Guid productId, string productName, int quantity, decimal unitPrice)
    {
        OrderId = orderId;
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}