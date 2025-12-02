using E_Commerce_Admin_Panel.Authorization;
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
    public class CustomersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public CustomersController(ApplicationDbContext db) => _db = db;

        private bool UserHasPermission(string permission) =>
            User.Claims.Any(c => c.Type == "permission" && c.Value == permission);

        [HttpGet]
        [HasPermission("Customer.View")]
        public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 200) pageSize = 200; // safety cap


            // Base query: exclude deleted
            var query = _db.Customers
                .AsNoTracking()
                .Where(c => !c.IsDelete);

            // Optional search: check name or email (case-insensitive)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(c => EF.Functions.Like(c.FullName, $"%{s}%")
                                      || EF.Functions.Like(c.Email, $"%{s}%"));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(c => c.FullName) // or other preferred sort
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    c.FullName,
                    c.Email,
                    c.Phone,
                    c.Address
                })
                .ToListAsync();

            return Ok(new
            {
                total,
                page,
                pageSize,
                items
            });
        }


        [HttpGet("{id:long}")]
        [HasPermission("Customer.View")]
        public async Task<IActionResult> Get(long id)
        {
            var c = await _db.Customers.FindAsync(id);
            if (c == null) return NotFound();
            return Ok(c);
        }

        [HttpPost]
        [HasPermission("Customer.Manage")]
        public async Task<IActionResult> Create([FromBody] CreateCustomerRequest dto)
        {
            if (!UserHasPermission("Customer.Manage")) return Forbid();

            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest("FullName and Email required");

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

            return CreatedAtAction(nameof(Get), new { id = cust.Id }, cust);
        }

        [HttpPut("{id:long}")]
        [HasPermission("Customer.Manage")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateCustomerRequest dto)
        {
            if (!UserHasPermission("Customer.Manage")) return Forbid();

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
        [HasPermission("Customer.Manage")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!UserHasPermission("Customer.Manage")) return Forbid();

            var c = await _db.Customers.FindAsync(id);
            if (c == null) return NotFound();

            c.IsDelete = true;
            c.LastModifiedAt = DateTimeOffset.UtcNow;
            c.LastModifiedBy = User.Identity?.Name ?? c.LastModifiedBy;
            await _db.SaveChangesAsync();
            return NoContent();
        }

    }
}
