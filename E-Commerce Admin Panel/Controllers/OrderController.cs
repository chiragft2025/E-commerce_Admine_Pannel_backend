using E_Commerce_Admin_Panel.Authorization;
using E_Commerce_Admin_Panel.Dtos;
using E_Commerce_Admin_Panel.Dtos.Order;
using InventoryAdmin.Domain.Entities;
using InventoryAdmin.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce_Admin_Panel.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : BaseApiController
    {
        private readonly ApplicationDbContext _db;
        public OrdersController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        [HasPermission("Order.View")]
        public async Task<ActionResult<PagedResult<OrderListItemDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 200) pageSize = 200;

            var q = _db.Orders
                .Include(o => o.Customer)
                .AsNoTracking()
                .Where(o => !o.IsDelete);

            if (!IsAdmin())
            {
                var currentUser = GetCurrentUsername();
                if (string.IsNullOrEmpty(currentUser))
                    return Forbid();

                q = q.Where(o => o.CreatedBy == currentUser);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                q = q.Where(o =>
                    o.Id.ToString().Contains(s) ||
                    o.Customer.FullName.ToLower().Contains(s));
            }

            q = q.OrderByDescending(o => o.PlacedAt);

            var total = await q.CountAsync();

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderListItemDto
                {
                    Id = o.Id,
                    PlacedAt = o.PlacedAt,
                    Status = o.Status.ToString(),
                    TotalAmount = o.TotalAmount,
                    Customer = new CustomerBriefDto
                    {
                        Id = o.Customer.Id,
                        FullName = o.Customer.FullName
                    },
                    CreatedBy = o.CreatedBy,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            var result = new PagedResult<OrderListItemDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                TotalPages = totalPages
            };

            return Ok(result);
        }

        [HttpGet("{id:long}")]
        [HasPermission("Order.View")]
        public async Task<ActionResult<OrderDto>> Get(long id)
        {
            var order = await _db.Orders
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .Include(o => o.Customer)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id && !o.IsDelete);

            if (order == null)
                return NotFound();

            if (!IsAdmin())
            {
                var currentUser = GetCurrentUsername();
                if (string.IsNullOrEmpty(currentUser))
                    return Forbid();

                if (!string.Equals(order.CreatedBy, currentUser, StringComparison.OrdinalIgnoreCase))
                    return Forbid();
            }

            var dto = new OrderDto
            {
                Id = order.Id,
                PlacedAt = order.PlacedAt,
                Status = order.Status.ToString(),
                TotalAmount = order.TotalAmount,
                Customer = new CustomerDetailDto
                {
                    Id = order.Customer.Id,
                    FullName = order.Customer.FullName,
                    Email = order.Customer.Email
                },
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Product = new ProductBriefDto
                    {
                        Id = i.Product.Id,
                        Name = i.Product.Name,
                        SKU = i.Product.SKU
                    }
                }).ToList(),
                CreatedBy = order.CreatedBy,
                CreatedAt = order.CreatedAt
            };

            return Ok(dto);
        }

        [HttpPost]
        [HasPermission("Order.Manage")]
        public async Task<ActionResult<CreateOrderResponseDto>> Create([FromBody] CreateOrderRequest dto)
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
                await _db.SaveChangesAsync(); // gets order.Id

                decimal total = 0m;
                foreach (var it in dto.Items)
                {
                    var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == it.Id && p.IsActive);
                    if (product == null) throw new Exception($"Product {it.Id} not found");
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

                var response = new CreateOrderResponseDto
                {
                    Id = order.Id,
                    TotalAmount = order.TotalAmount
                };

                return CreatedAtAction(nameof(Get), new { id = order.Id }, response);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                // Consider returning a structured error object; keeping your pattern here
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
