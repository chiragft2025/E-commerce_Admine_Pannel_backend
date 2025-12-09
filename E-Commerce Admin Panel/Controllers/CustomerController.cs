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
            if (pageSize > 200) pageSize = 200;

            // Base query (exclude deleted)
            var query = _db.Customers
                .AsNoTracking()
                .Where(c => !c.IsDelete);

            // Ownership filter — admins see all, users see only their own customers
            if (!IsAdmin())
            {
                var currentUser = GetCurrentUsername();
                if (string.IsNullOrEmpty(currentUser))
                    return Forbid();

                // If CreatedBy is a username
                query = query.Where(c => c.CreatedBy == currentUser);

                // ---- If CreatedBy is numeric (CreatedById), use this instead ----
                /*
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(idClaim) || !long.TryParse(idClaim, out var currUserId))
                    return Forbid();

                query = query.Where(c => c.CreatedById == currUserId);
                */
            }

            // Search filter
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
                .Select(c => new
                {
                    c.Id,
                    c.FullName,
                    c.Email,
                    c.Phone,
                    c.Address,
                    c.CreatedBy,
                    c.CreatedAt
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

        private string? GetCurrentUsername()
        {
            if (User?.Identity?.IsAuthenticated != true) return null;

            if (!string.IsNullOrEmpty(User.Identity?.Name))
                return User.Identity.Name;

            // common claim names
            return User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                   ?? User.FindFirst("name")?.Value
                   ?? User.FindFirst("preferred_username")?.Value
                   ?? User.FindFirst("username")?.Value;
        }

        private bool IsAdmin()
        {
            if (User == null) return false;

            // common role checks
            if (User.IsInRole("Admin")) return true;

            var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value)
                        .Concat(User.FindAll("role").Select(c => c.Value));

            return roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));
        }



        [HttpGet("{id:long}")]
        [HasPermission("Customer.View")]
        public async Task<IActionResult> Get(long id)
        {
            var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);
            if (c == null) return NotFound();

            // Ownership check — only admin or the creator can view
            if (!IsAdmin())
            {
                var currentUser = GetCurrentUsername();
                if (string.IsNullOrEmpty(currentUser))
                    return Forbid();

                // If CreatedBy is stored as username
                if (!string.Equals(c.CreatedBy, currentUser, StringComparison.OrdinalIgnoreCase))
                    return Forbid();

                // ---- If CreatedBy is numeric (CreatedById) use this instead ----
                /*
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(idClaim) || !long.TryParse(idClaim, out var currUserId))
                    return Forbid();

                if (c.CreatedById != currUserId)
                    return Forbid();
                */
            }

            // What you return is up to you — here returning only safe fields:
            return Ok(new
            {
                c.Id,
                c.FullName,
                c.Email,
                c.Phone,
                c.Address,
                c.CreatedBy,
                c.CreatedAt
            });
        }


        [HttpPost]
        [HasPermission("Customer.Create")]
        public async Task<IActionResult> Create([FromBody] CreateCustomerRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest("FullName and Email required");

            // 🔍 Check FullName duplicate (case-insensitive)
            var nameExists = await _db.Customers
                .AnyAsync(x => x.FullName.ToLower() == dto.FullName.ToLower());

            if (nameExists)
                return BadRequest("Customer full name already exists");

            // 🔍 Check Email duplicate (case-insensitive)
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

            return CreatedAtAction(nameof(Get), new { id = cust.Id }, cust);
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

            // ❗ Check if customer is referenced in any order
            bool hasOrders = await _db.Orders.AnyAsync(o => o.CustomerId == id);

            if (hasOrders)
            {
                return Conflict(new
                {
                    message = "Cannot delete customer because they are referenced in existing orders."
                });
            }

            // Soft delete
            c.IsDelete = true;
            c.LastModifiedAt = DateTimeOffset.UtcNow;
            c.LastModifiedBy = User.Identity?.Name ?? c.LastModifiedBy;

            await _db.SaveChangesAsync();

            return NoContent();
        }

    }
}
