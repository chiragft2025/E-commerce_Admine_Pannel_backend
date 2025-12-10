using E_Commerce_Admin_Panel.Authorization;
using E_Commerce_Admin_Panel.Dtos;
using E_Commerce_Admin_Panel.Dtos.Customer;
using InventoryAdmin.Domain.Entities;
using InventoryAdmin.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce_Admin_Panel.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomersController : BaseApiController
    {
        private readonly ApplicationDbContext _db;
        public CustomersController(ApplicationDbContext db) => _db = db;


        [HttpGet]
        [HasPermission("Customer.View")]
        public async Task<ActionResult<PagedResult<CustomerListItemDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 200) pageSize = 200;

            var query = _db.Customers
                .AsNoTracking()
                .Where(c => !c.IsDelete);

            if (!IsAdmin())
            {
                var currentUser = GetCurrentUsername();
                if (string.IsNullOrEmpty(currentUser))
                    return Forbid();

                query = query.Where(c => c.CreatedBy == currentUser);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(c =>
                    EF.Functions.Like(c.FullName, $"%{s}%") ||
                    EF.Functions.Like(c.Email, $"%{s}%"));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(c => c.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CustomerListItemDto
                {
                    Id = c.Id,
                    FullName = c.FullName,
                    Email = c.Email,
                    Phone = c.Phone,
                    Address = c.Address,
                    CreatedBy = c.CreatedBy,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);

            var result = new PagedResult<CustomerListItemDto>
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
        [HasPermission("Customer.View")]
        public async Task<ActionResult<CustomerDto>> Get(long id)
        {
            var c = await _db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);

            if (c == null) return NotFound();

            if (!IsAdmin())
            {
                var currentUser = GetCurrentUsername();
                if (string.IsNullOrEmpty(currentUser))
                    return Forbid();

                if (!string.Equals(c.CreatedBy, currentUser, StringComparison.OrdinalIgnoreCase))
                    return Forbid();
            }

            var dto = new CustomerDto
            {
                Id = c.Id,
                FullName = c.FullName,
                Email = c.Email,
                Phone = c.Phone,
                Address = c.Address,
                CreatedBy = c.CreatedBy,
                CreatedAt = c.CreatedAt,
                LastModifiedBy = c.LastModifiedBy,
                LastModifiedAt = c.LastModifiedAt
            };

            return Ok(dto);
        }


        [HttpPost]
        [HasPermission("Customer.Create")]
        public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest("FullName and Email required");

            var nameExists = await _db.Customers
                .AnyAsync(x => x.FullName.ToLower() == dto.FullName.ToLower());

            if (nameExists)
                return BadRequest("Customer full name already exists");

            var emailExists = await _db.Customers
                .AnyAsync(x => x.Email.ToLower() == dto.Email.ToLower());

            if (emailExists)
                return BadRequest("Customer email already exists");

            var cust = new Customer
            {
                FullName = dto.FullName,
                Email = dto.Email,
                Phone = dto.Phone,
                Address = dto.Address,
                CreatedBy = User.Identity?.Name ?? "system",
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.Customers.Add(cust);
            await _db.SaveChangesAsync();

            var resultDto = new CustomerDto
            {
                Id = cust.Id,
                FullName = cust.FullName,
                Email = cust.Email,
                Phone = cust.Phone,
                Address = cust.Address,
                CreatedBy = cust.CreatedBy,
                CreatedAt = cust.CreatedAt
            };

            return CreatedAtAction(nameof(Get), new { id = cust.Id }, resultDto);
        }



        [HttpPut("{id:long}")]
        [HasPermission("Customer.Edit")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateCustomerRequest dto)
        {
            var c = await _db.Customers.FindAsync(id);
            if (c == null) return NotFound();

            c.FullName = dto.FullName ?? c.FullName;
            c.Email = dto.Email ?? c.Email;
            c.Phone = dto.Phone ?? c.Phone;
            c.Address = dto.Address ?? c.Address;
            c.LastModifiedAt = DateTimeOffset.UtcNow;
            c.LastModifiedBy = User.Identity?.Name ?? c.LastModifiedBy;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:long}")]
        [HasPermission("Customer.Delete")]
        public async Task<IActionResult> Delete(long id)
        {
            var c = await _db.Customers.FindAsync(id);
            if (c == null)
                return NotFound();

            bool hasOrders = await _db.Orders.AnyAsync(o => o.CustomerId == id);

            if (hasOrders)
            {
                // return a typed error object (optional)
                return Conflict(new { message = "Cannot delete customer because they are referenced in existing orders." });
            }

            c.IsDelete = true;
            c.LastModifiedAt = DateTimeOffset.UtcNow;
            c.LastModifiedBy = User.Identity?.Name ?? c.LastModifiedBy;

            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
