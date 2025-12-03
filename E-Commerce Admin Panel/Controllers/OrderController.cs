using E_Commerce_Admin_Panel.Authorization;
using E_Commerce_Admin_Panel.Dtos.Order;
using InventoryAdmin.Domain.Entities;
using InventoryAdmin.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce_Admin_Panel.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public OrdersController(ApplicationDbContext db) => _db = db;

        private bool UserHasPermission(string permission) =>
            User.Claims.Any(c => c.Type == "permission" && c.Value == permission);

        [HttpGet]
        [HasPermission("Order.View")]
        public async Task<IActionResult> GetAll(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string? search = null)
        {
            var q = _db.Orders
                .Include(o => o.Customer)
                .AsNoTracking();

            // Apply search if provided
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                q = q.Where(o =>
                    o.Id.ToString().Contains(search) ||
                    o.Customer.FullName.ToLower().Contains(search)
                );
            }

            q = q.OrderByDescending(o => o.PlacedAt);

            var total = await q.CountAsync();

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    o.Id,
                    o.PlacedAt,
                    o.Status,
                    o.TotalAmount,
                    Customer = new
                    {
                        o.Customer.Id,
                        o.Customer.FullName
                    }
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        [HttpGet("{id:long}")]
        [HasPermission("Order.View")]
        public async Task<IActionResult> Get(long id)
        {
            var order = await _db.Orders
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return Ok(order);
        }

        [HttpPost]
        [HasPermission("Order.Manage")]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest dto)
        {
            var customer = await _db.Customers.FindAsync(dto.CustomerId);
            if (customer == null) return BadRequest("Invalid customer");

            if (dto.Items == null || !dto.Items.Any()) return BadRequest("Order items required");

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = new Order
                {
                    CustomerId = dto.CustomerId,
                    PlacedAt = DateTimeOffset.UtcNow,
                    Status = OrderStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "system",
                    ShippingAddress = dto.ShippingAddress
                };

                _db.Orders.Add(order);
                await _db.SaveChangesAsync(); // get order.Id

                decimal total = 0m;
                foreach (var it in dto.Items)
                {
                    var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == it.ProductId && p.IsActive);
                    if (product == null) throw new Exception($"Product {it.ProductId} not found");
                    if (product.Stock < it.Quantity) throw new Exception($"Insufficient stock for product {product.Name}");

                    var unitPrice = product.Price;
                    product.Stock -= it.Quantity;
                    _db.Products.Update(product);

                    var orderItem = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = product.Id,
                        Quantity = it.Quantity,
                        UnitPrice = unitPrice,
                        CreatedAt = DateTimeOffset.UtcNow,
                        CreatedBy = order.CreatedBy
                    };

                    _db.OrderItems.Add(orderItem);
                    total += unitPrice * it.Quantity;
                }

                order.TotalAmount = total;
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return CreatedAtAction(nameof(Get), new { id = order.Id }, new { order.Id, order.TotalAmount });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id:long}/cancel")]
        [HasPermission("Order.Manage")]
        public async Task<IActionResult> Cancel(long id)
        {
            var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
                return BadRequest("Cannot cancel an order that is shipped or delivered");

            // revert stock
            foreach (var item in order.Items)
            {
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.Stock += item.Quantity;
                    _db.Products.Update(product);
                }
            }

            order.Status = OrderStatus.Cancelled;
            order.LastModifiedAt = DateTimeOffset.UtcNow;
            order.LastModifiedBy = User.Identity?.Name ?? order.LastModifiedBy;

            await _db.SaveChangesAsync();
            return NoContent();
        }

    }
}
