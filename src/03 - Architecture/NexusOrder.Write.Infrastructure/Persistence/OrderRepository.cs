using Microsoft.EntityFrameworkCore;
using NexusOrder.Write.Application.Abstractions.Persistence;
using NexusOrder.Write.Domain.Orders;

namespace NexusOrder.Write.Infrastructure.Persistence;

public sealed class OrderRepository(WriteDbContext dbContext) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await dbContext.Orders.AddAsync(order, cancellationToken);
    }
}