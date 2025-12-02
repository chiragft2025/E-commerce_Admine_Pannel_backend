using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryAdmin.Domain.Entities
{
    public class Order : BaseEntity
    {
        public DateTimeOffset PlacedAt { get; set; } = DateTimeOffset.UtcNow;

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        // FK -> Customer
        public long CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

        public decimal TotalAmount { get; set; }
        public string? ShippingAddress { get; set; }
    }
}
