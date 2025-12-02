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

            // Base query
            var query = _db.Categories.AsQueryable();

            // Filter by category id if provided
            if (categoryId.HasValue)
            {
                query = query.Where(c => c.Id == categoryId.Value);
            }

            // Filter by search if provided (case-insensitive search on Title and Description)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLowerInvariant();
                // Use ToLower on DB fields; EF Core will translate this for common providers.
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
                    c.Description
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

        // GET: api/Categories/{id}
        [HttpGet("{id:long}")]
        [HasPermission("Category.View")]
        public async Task<IActionResult> Get(long id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return NotFound();
            return Ok(new { cat.Id, cat.Title, cat.Description });
        }

        [HttpPost]
        [HasPermission("Category.Manage")]
        public async Task<IActionResult> Create([FromBody] CreateCategoryRequest dto)
        {
            if (!UserHasPermission("Category.Manage") && !UserHasPermission("Product.Create"))
                return Forbid();

            if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest("Title required");

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
        [HasPermission("Category.Manage")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateCategoryRequest dto)
        {
            if (!UserHasPermission("Category.Manage")) return Forbid();

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
        [HasPermission("Category.Manage")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!UserHasPermission("Category.Manage")) return Forbid();

            var c = await _db.Categories.Include(x => x.Products).FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            // Soft delete
            c.IsDelete = true;
            c.LastModifiedAt = DateTimeOffset.UtcNow;
            c.LastModifiedBy = User.Identity?.Name ?? c.LastModifiedBy;
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
