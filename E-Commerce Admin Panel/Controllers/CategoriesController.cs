using E_Commerce_Admin_Panel.Authorization;
using E_Commerce_Admin_Panel.Dtos.Category;
using InventoryAdmin.Domain.Entities;
using InventoryAdmin.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce_Admin_Panel.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public CategoriesController(ApplicationDbContext db) => _db = db;

        // Helper to check permission claim
        private bool UserHasPermission(string permission) =>
            User.Claims.Any(c => c.Type == "permission" && c.Value == permission);

        [HttpGet]
        [HasPermission("Category.View")]
        public async Task<IActionResult> GetAll(
     [FromQuery] int page = 1,
     [FromQuery] int pageSize = 10,
     [FromQuery] long? categoryId = null,
     [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 10;

            // Base query (exclude soft-deleted if you use IsDelete)
            var query = _db.Categories.AsQueryable().Where(c => c.IsDelete == false);

            // Ownership filter: non-admins should only see categories they created
            if (!IsAdmin())
            {
                var currentUser = GetCurrentUsername();
                if (string.IsNullOrEmpty(currentUser))
                    return Forbid(); // or Unauthorized()

                // CreatedBy is assumed to be stored as username (string).
                query = query.Where(c => c.CreatedBy == currentUser);
            }

            // Filter by category id if provided
            if (categoryId.HasValue)
            {
                query = query.Where(c => c.Id == categoryId.Value);
            }

            // Filter by search if provided (case-insensitive search on Title and Description)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLowerInvariant();
                query = query.Where(c =>
                    c.Title.ToLower().Contains(searchLower) ||
                    (c.Description != null && c.Description.ToLower().Contains(searchLower)));
            }

            // Get total count before paging
            var totalCount = await query.CountAsync();

            // Apply ordering, paging and projection to DTO
            var items = await query
                .OrderBy(c => c.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.Description,
                    c.CreatedBy,
                    c.CreatedAt
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var result = new
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            return Ok(result);
        }

        // ---------- Helpers to add inside the same controller (or shared base) ----------

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
        [HasPermission("Category.View")]
        public async Task<IActionResult> Get(long id)
        {
            var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.IsDelete == false);
            if (cat == null) return NotFound();

            // Ownership check - only owner or admin can view
            if (!IsAdmin())
            {
                var currentUser = GetCurrentUsername();
                if (string.IsNullOrEmpty(currentUser))
                    return Forbid();

                if (cat.CreatedBy != currentUser)
                    return Forbid(); // User is NOT owner → access denied
            }

            return Ok(new
            {
                cat.Id,
                cat.Title,
                cat.Description
            });
        }

        [HttpPost]
        [HasPermission("Category.Create")]
        public async Task<IActionResult> Create([FromBody] CreateCategoryRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest("Title required");

            // Check duplicate title (case-insensitive)
            var exists = await _db.Categories
                .AnyAsync(x => x.Title.ToLower() == dto.Title.ToLower());

            if (exists)
                return BadRequest("Category title already exists");

            var c = new Category
            {
                Title = dto.Title,
                Description = dto.Description,
                CreatedBy = User.Identity?.Name ?? "system",
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.Categories.Add(c);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = c.Id }, new { c.Id, c.Title, c.Description });
        }

        [HttpPut("{id:long}")]
        [HasPermission("Category.Edit")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateCategoryRequest dto)
        {

            var c = await _db.Categories.FindAsync(id);
            if (c == null) return NotFound();

            c.Title = dto.Title ?? c.Title;
            c.Description = dto.Description ?? c.Description;
            c.LastModifiedAt = DateTimeOffset.UtcNow;
            c.LastModifiedBy = User.Identity?.Name ?? c.LastModifiedBy;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:long}")]
        [HasPermission("Category.Delete")]
        public async Task<IActionResult> Delete(long id)
        {
            var c = await _db.Categories
                             .Include(x => x.Products)
                             .FirstOrDefaultAsync(x => x.Id == id);

            if (c == null)
                return NotFound();

            var hasActiveProducts = c.Products.Any(p => !p.IsDelete);
            if (hasActiveProducts)
            {
                // Throw and let your global exception handler convert to a response
                throw new InvalidOperationException("Category cannot be deleted because it is used by one or more products.");
            }

            c.IsDelete = true;
            c.LastModifiedAt = DateTimeOffset.UtcNow;
            c.LastModifiedBy = User?.Identity?.Name ?? c.LastModifiedBy;

            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
