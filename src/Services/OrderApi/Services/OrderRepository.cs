using System.Collections.Concurrent;
using SharedContracts;

namespace OrderApi.Services;

public interface IOrderRepository
{
    IEnumerable<OrderDto> GetAll(string? customerEmail = null);
    OrderDto? GetById(Guid id);
    OrderDto Create(OrderDto order);
    OrderDto? UpdateStatus(Guid id, string newStatus);
}

public class OrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<Guid, OrderDto> _orders = new();

    public IEnumerable<OrderDto> GetAll(string? customerEmail = null)
    {
        var query = _orders.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            query = query.Where(o => o.CustomerEmail.Equals(customerEmail, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(o => o.CreatedAt);
    }

    public OrderDto? GetById(Guid id)
    {
        _orders.TryGetValue(id, out var order);
        return order;
    }

    public OrderDto Create(OrderDto order)
    {
        _orders[order.Id] = order;
        return order;
    }

    public OrderDto? UpdateStatus(Guid id, string newStatus)
    {
        if (_orders.TryGetValue(id, out var order))
        {
            var updated = order with { Status = newStatus };
            _orders[id] = updated;
            return updated;
        }

        return null;
    }
}
