using E_Commerce_Admin_Panel.Authorization;
using E_Commerce_Admin_Panel.Dtos.Product;
using InventoryAdmin.Domain.Entities;
using InventoryAdmin.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce_Admin_Panel.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : BaseApiController
    {
        private readonly ApplicationDbContext _db;
        public ProductsController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        [HasPermission("Product.View")]
        public async Task<ActionResult<E_Commerce_Admin_Panel.Dtos.PagedResult<ProductListItemDto>>> GetAll(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
            [FromQuery] long? categoryId = null, [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 10;

            var query = _db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
                .Where(p => p.IsActive && !p.IsDelete);

            if (!IsAdmin())
            {
                var currentUser = GetCurrentUsername();
                if (string.IsNullOrEmpty(currentUser))
                    return Forbid();

                query = query.Where(p => p.CreatedBy == currentUser);
            }

            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(p => p.Name.Contains(s));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductListItemDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    Price = p.Price,
                    Stock = p.Stock,
                    IsActive = p.IsActive,
                    Category = p.Category == null ? null : new CategoryBriefDto { Id = p.Category.Id, Title = p.Category.Title },
                    Tags = p.ProductTags.Select(pt => new TagDto { TagId = pt.TagId, Name = pt.Tag.Name }).ToList(),
                    CreatedBy = p.CreatedBy,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            var result = new E_Commerce_Admin_Panel.Dtos.PagedResult<ProductListItemDto>
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
        [HasPermission("Product.View")]
        public async Task<ActionResult<ProductDto>> Get(long id)
        {
            var p = await _db.Products
                .Include(x => x.Category)
                .Include(x => x.ProductTags).ThenInclude(pt => pt.Tag)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);

            if (p == null) return NotFound();

            if (!IsAdmin())
            {
                var currentUser = GetCurrentUsername();
                if (string.IsNullOrEmpty(currentUser))
                    return Forbid();

                if (!string.Equals(p.CreatedBy, currentUser, StringComparison.OrdinalIgnoreCase))
                    return Forbid();
            }

            var dto = new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                SKU = p.SKU,
                Description = p.Description,
                Price = p.Price,
                Stock = p.Stock,
                IsActive = p.IsActive,
                Category = p.Category == null ? null : new CategoryBriefDto { Id = p.Category.Id, Title = p.Category.Title },
                Tags = p.ProductTags.Select(pt => new TagDto { TagId = pt.TagId, Name = pt.Tag.Name }).ToList(),
                CreatedBy = p.CreatedBy,
                CreatedAt = p.CreatedAt,
                LastModifiedBy = p.LastModifiedBy,
                LastModifiedAt = p.LastModifiedAt
            };

            return Ok(dto);
        }

        [HttpPost]
        [HasPermission("Product.Create")]
        public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest dto)
        {
            if (dto == null) return BadRequest("Payload required");
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name is required");
            if (dto.Price < 0) return BadRequest("Price must be >= 0");
            if (dto.Stock < 0) return BadRequest("Stock must be >= 0");

            var category = await _db.Categories.FindAsync(dto.CategoryId);
            if (category == null) return BadRequest("Invalid category id");

            var nameToCheck = dto.Name.Trim();
            var nameExists = await _db.Products.AnyAsync(p => p.Name.ToLower() == nameToCheck.ToLower());
            if (nameExists) return BadRequest("Product name already exists");

            // Normalize tags (same logic as before) — produce HashSet<string> uniqueTagNames
            List<string> incomingTagNames = new();
            if (dto.Tags != null)
            {
                incomingTagNames = dto.Tags
                    .Select(x => x?.Name?.Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
            }
            var uniqueTagNames = new HashSet<string>(incomingTagNames, StringComparer.OrdinalIgnoreCase);

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var product = new Product
                {
                    Name = dto.Name.Trim(),
                    SKU = string.IsNullOrWhiteSpace(dto.SKU) ? Guid.NewGuid().ToString("N").Substring(0, 10) : dto.SKU.Trim(),
                    Description = dto.Description,
                    Price = dto.Price,
                    Stock = dto.Stock,
                    IsActive = dto.IsActive,
                    CategoryId = dto.CategoryId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "system"
                };

                _db.Products.Add(product);
                await _db.SaveChangesAsync(); // obtain product.Id

                if (uniqueTagNames.Count > 0)
                {
                    var lowerNames = uniqueTagNames.Select(n => n.ToLowerInvariant()).ToList();

                    var existingTags = await _db.Tags
                        .Where(t => !t.IsDelete && lowerNames.Contains(t.Name.ToLower()))
                        .ToListAsync();

                    var existingByLower = existingTags
                        .ToDictionary(t => t.Name.ToLowerInvariant(), t => t, StringComparer.OrdinalIgnoreCase);

                    var tagsToAdd = new List<Tag>();
                    foreach (var name in uniqueTagNames)
                    {
                        var nameLower = name.ToLowerInvariant();
                        if (!existingByLower.ContainsKey(nameLower))
                        {
                            var newTag = new Tag
                            {
                                Name = name,
                                CreatedAt = DateTimeOffset.UtcNow,
                                CreatedBy = product.CreatedBy
                            };
                            tagsToAdd.Add(newTag);
                            existingByLower[nameLower] = newTag;
                        }
                    }

                    if (tagsToAdd.Count > 0)
                    {
                        _db.Tags.AddRange(tagsToAdd);
                        await _db.SaveChangesAsync();
                    }

                    var productTags = new List<ProductTag>();
                    foreach (var kv in existingByLower)
                    {
                        var tagEntity = kv.Value;
                        var existsJoin = await _db.ProductTags.FindAsync(product.Id, tagEntity.Id);
                        if (existsJoin == null)
                        {
                            productTags.Add(new ProductTag { ProductId = product.Id, TagId = tagEntity.Id });
                        }
                    }

                    if (productTags.Count > 0)
                    {
                        _db.ProductTags.AddRange(productTags);
                        await _db.SaveChangesAsync();
                    }
                }

                await tx.CommitAsync();

                // Build DTO to return (do not return EF entity)
                var createdDto = new ProductDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    SKU = product.SKU,
                    Description = product.Description,
                    Price = product.Price,
                    Stock = product.Stock,
                    IsActive = product.IsActive,
                    Category = new CategoryBriefDto { Id = category.Id, Title = category.Title },
                    Tags = uniqueTagNames.Select((n, i) => new TagDto { TagId = 0, Name = n }).ToList(), // TagId may be 0 for newly created; clients usually re-fetch full resource
                    CreatedBy = product.CreatedBy,
                    CreatedAt = product.CreatedAt
                };

                return CreatedAtAction(nameof(Get), new { id = product.Id }, createdDto);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { message = "Failed to create product", detail = ex.Message });
            }
        }

        [HttpPut("{id:long}")]
        [HasPermission("Product.Edit")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateProductRequest dto)
        {
            var p = await _db.Products
                .Include(x => x.ProductTags).ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);

            if (p == null) return NotFound();

            if (dto.Price.HasValue && dto.Price < 0) return BadRequest("Price must be >= 0");
            if (dto.Stock.HasValue && dto.Stock < 0) return BadRequest("Stock must be >= 0");

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                p.Name = dto.Name ?? p.Name;
                p.SKU = dto.SKU ?? p.SKU;
                p.Description = dto.Description ?? p.Description;
                p.Price = dto.Price ?? p.Price;
                p.Stock = dto.Stock ?? p.Stock;
                p.IsActive = dto.IsActive ?? p.IsActive;

                if (dto.CategoryId.HasValue)
                {
                    var cat = await _db.Categories.FindAsync(dto.CategoryId.Value);
                    if (cat == null) return BadRequest("Invalid category id");
                    p.CategoryId = dto.CategoryId.Value;
                }

                p.LastModifiedAt = DateTimeOffset.UtcNow;
                p.LastModifiedBy = User.Identity?.Name ?? p.LastModifiedBy;

                if (dto.Tags != null)
                {
                    var existingLinks = p.ProductTags.ToList();
                    if (existingLinks.Count > 0) _db.ProductTags.RemoveRange(existingLinks);

                    var incoming = dto.Tags
                        .Select(x => x?.Name?.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (incoming.Count > 0)
                    {
                        var lowerNames = incoming.Select(n => n.ToLowerInvariant()).ToList();

                        var existingTags = await _db.Tags
                            .Where(t => !t.IsDelete && lowerNames.Contains(t.Name.ToLower()))
                            .ToListAsync();

                        var existingByLower = existingTags.ToDictionary(
                            x => x.Name.ToLowerInvariant(),
                            x => x,
                            StringComparer.OrdinalIgnoreCase
                        );

                        var tagsToAdd = new List<Tag>();
                        foreach (var name in incoming)
                        {
                            var lower = name.ToLowerInvariant();
                            if (!existingByLower.ContainsKey(lower))
                            {
                                var newTag = new Tag
                                {
                                    Name = name,
                                    CreatedAt = DateTimeOffset.UtcNow,
                                    CreatedBy = p.LastModifiedBy ?? p.CreatedBy
                                };
                                tagsToAdd.Add(newTag);
                                existingByLower[lower] = newTag;
                            }
                        }

                        if (tagsToAdd.Count > 0)
                        {
                            _db.Tags.AddRange(tagsToAdd);
                            await _db.SaveChangesAsync();
                        }

                        var productTags = existingByLower.Values
                            .Select(tag => new ProductTag { ProductId = p.Id, TagId = tag.Id })
                            .ToList();

                        _db.ProductTags.AddRange(productTags);
                    }
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { message = "Failed to update product", detail = ex.Message });
            }
        }

        [HttpDelete("{id:long}")]
        [HasPermission("Product.Delete")]
        public async Task<IActionResult> Delete(long id)
        {
            var p = await _db.Products.FindAsync(id);
            if (p == null) return NotFound();

            var isUsed = await _db.OrderItems.AnyAsync(oi => oi.ProductId == id);
            if (isUsed)
            {
                return Conflict(new { message = "Cannot delete product because it is referenced by existing orders." });
            }

            p.IsDelete = true;
            p.LastModifiedAt = DateTimeOffset.UtcNow;
            p.LastModifiedBy = User.Identity?.Name ?? p.LastModifiedBy;

            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
