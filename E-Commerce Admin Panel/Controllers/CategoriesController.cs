using E_Commerce_Admin_Panel.Authorization;
using E_Commerce_Admin_Panel.Dtos;
using E_Commerce_Admin_Panel.Dtos.Category;
using InventoryAdmin.Domain.Entities;
using InventoryAdmin.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce_Admin_Panel.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : BaseApiController
    {
        private readonly ApplicationDbContext _db;
        public CategoriesController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        [HasPermission("Category.View")]
        public async Task<ActionResult<PagedResult<CategoryListItemDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] long? categoryId = null,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 10;

            var query = _db.Categories.AsQueryable().Where(c => c.IsDelete == false);

            if (!IsAdmin())
            {
                var currentUser = GetCurrentUsername();
                if (string.IsNullOrEmpty(currentUser))
                    return Forbid();

                query = query.Where(c => c.CreatedBy == currentUser);
            }

            if (categoryId.HasValue)
                query = query.Where(c => c.Id == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLowerInvariant();
                query = query.Where(c =>
                    c.Title.ToLower().Contains(searchLower) ||
                    (c.Description != null && c.Description.ToLower().Contains(searchLower)));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(c => c.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CategoryListItemDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    CreatedBy = c.CreatedBy,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var result = new PagedResult<CategoryListItemDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            return Ok(result);
        }

        [HttpGet("{id:long}")]
        [HasPermission("Category.View")]
        public async Task<ActionResult<CategoryDto>> Get(long id)
        {
            var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.IsDelete == false);
            if (cat == null) return NotFound();

            if (!IsAdmin())
            {
                var currentUser = GetCurrentUsername();
                if (string.IsNullOrEmpty(currentUser))
                    return Forbid();

                if (cat.CreatedBy != currentUser)
                    return Forbid();
            }

            var dto = new CategoryDto
            {
                Id = cat.Id,
                Title = cat.Title,
                Description = cat.Description,
                CreatedBy = cat.CreatedBy,
                CreatedAt = cat.CreatedAt,
                LastModifiedAt = cat.LastModifiedAt,
                LastModifiedBy = cat.LastModifiedBy
            };

            return Ok(dto);
        }

        [HttpPost]
        [HasPermission("Category.Create")]
        public async Task<ActionResult<CategoryDto>> Create([FromBody] CreateCategoryRequest dto)
        {
            // normalize input
            var title = dto?.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
                return BadRequest("Title required");

            // determine current user (same logic you used when creating the Category)
            var username = User?.Identity?.Name ?? "system";

            // normalize for case-insensitive comparison
            var titleLower = title.ToLowerInvariant();
            var createdByLower = (username ?? "system").ToLowerInvariant();
            bool exists;
            if (!IsAdmin())
            {
                 exists = await _db.Categories
                .AnyAsync(x =>
                    x.Title != null &&
                    x.CreatedBy != null &&
                    x.Title.ToLower() == titleLower &&
                    x.CreatedBy.ToLower() == createdByLower);
            }
            else
            {
                 exists = await _db.Categories.AnyAsync(x => x.Title.ToLower() == dto.Title.ToLower());
            }

            // check existence only for categories created by the current user


            if (exists)
                return BadRequest("Category title already exists");

            var c = new Category
            {
                Title = title,
                Description = dto.Description,
                CreatedBy = username,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.Categories.Add(c);
            await _db.SaveChangesAsync();

            var resultDto = new CategoryDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                CreatedBy = c.CreatedBy,
                CreatedAt = c.CreatedAt
            };

            return CreatedAtAction(nameof(Get), new { id = c.Id }, resultDto);
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
