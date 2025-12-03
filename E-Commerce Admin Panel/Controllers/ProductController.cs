using E_Commerce_Admin_Panel.Authorization;
using E_Commerce_Admin_Panel.Dtos.Product;
using InventoryAdmin.Domain.Entities;
using InventoryAdmin.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce_Admin_Panel.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public ProductsController(ApplicationDbContext db) => _db = db;

        // Simple helper to check permission claims (permission claims are named "permission")
        private bool UserHasPermission(string permission) =>
            User?.Claims?.Any(c => string.Equals(c.Type, "permission", StringComparison.OrdinalIgnoreCase)
                                   && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase))
            ?? false;

        // GET: api/products?page=1&pageSize=10&categoryId=1&search=term
        [HttpGet]
        [HasPermission("Product.View")]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10,
            [FromQuery] long? categoryId = null, [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 10;

            var query = _db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
                .Where(p => p.IsActive && !p.IsDelete);

            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.Name.Contains(search));

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.SKU,
                    p.Price,
                    p.Stock,
                    p.IsActive,
                    Category = p.Category == null ? null : new { p.Category.Id, p.Category.Title },
                    Tags = p.ProductTags.Select(pt => new { pt.TagId, pt.Tag.Name })
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        [HttpGet("{id:long}")]
        [HasPermission("Product.View")]
        public async Task<IActionResult> Get(long id)
        {
            var p = await _db.Products
                .Include(x => x.Category)
                .Include(x => x.ProductTags).ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);

            if (p == null) return NotFound();

            return Ok(new
            {
                p.Id,
                p.Name,
                p.SKU,
                p.Description,
                p.Price,
                p.Stock,
                p.IsActive,
                Category = p.Category == null ? null : new { p.Category.Id, p.Category.Title },
                Tags = p.ProductTags.Select(pt => new { pt.TagId, pt.Tag.Name })
            });
        }

        [HttpPost]
        [HasPermission("Product.Manage")]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest dto)
        {
            if (dto == null) return BadRequest("Payload required");
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name is required");
            if (dto.Price < 0) return BadRequest("Price must be >= 0");
            if (dto.Stock < 0) return BadRequest("Stock must be >= 0");

            var category = await _db.Categories.FindAsync(dto.CategoryId);
            if (category == null) return BadRequest("Invalid category id");

            var nameToCheck = dto.Name.Trim();
            var nameExists = await _db.Products
                .AnyAsync(p => p.Name.ToLower() == nameToCheck.ToLower());
            if (nameExists)
                return BadRequest("Product name already exists");

            // --- SAFE: extract incoming tag names regardless of concrete DTO type ---
            List<string> incomingTagNames;
            if (dto.Tags == null)
            {
                incomingTagNames = new List<string>();
            }
            else if (dto.Tags is IEnumerable<ProductTagDto> tagDtoEnumerable) // adjust type name if needed
            {
                incomingTagNames = tagDtoEnumerable
                    .Select(x => x?.Name?.Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
            }
            else if (dto.Tags is IEnumerable<ProductTagDto> productTagDtoEnumerable) // adjust type name if needed
            {
                incomingTagNames = productTagDtoEnumerable
                    .Select(x => x?.Name?.Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
            }
            else
            {
                // fallback: attempt to read name via weakly-typed route (handles unexpected shapes)
                incomingTagNames = (dto.Tags as IEnumerable<object> ?? Enumerable.Empty<object>())
                    .Select(x =>
                    {
                        try
                        {
                            // dynamic access — safe inside try/catch
                            var dyn = x as dynamic;
                            string nm = (dyn?.Name ?? (dyn?.name ?? null))?.ToString();
                            return nm?.Trim();
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
            }

            // dedupe case-insensitively
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

                // Handle tags if provided
                if (uniqueTagNames.Count > 0)
                {
                    var lowerNames = uniqueTagNames.Select(n => n.ToLowerInvariant()).ToList();

                    // Fetch existing tags matching any of these names (case-insensitive)
                    var existingTags = await _db.Tags
                        .Where(t => !t.IsDelete && lowerNames.Contains(t.Name.ToLower()))
                        .ToListAsync();

                    var existingByLower = existingTags
                        .ToDictionary(t => t.Name.ToLowerInvariant(), t => t, StringComparer.OrdinalIgnoreCase);

                    var tagsToAdd = new List<Tag>();

                    // Create new Tag entities for missing names
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
                            existingByLower[nameLower] = newTag; // include in lookup so joins below work
                        }
                    }

                    if (tagsToAdd.Count > 0)
                    {
                        _db.Tags.AddRange(tagsToAdd);
                        await _db.SaveChangesAsync(); // so newTag.Id values are populated
                    }

                    // Build ProductTag join records for all tags (existing + newly created)
                    var productTags = new List<ProductTag>();
                    foreach (var kv in existingByLower)
                    {
                        var tagEntity = kv.Value;
                        // avoid duplicate join if already exists
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

                return CreatedAtAction(nameof(Get), new { id = product.Id }, new { product.Id, product.Name, product.SKU });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                // Consider logging the exception here
                return StatusCode(500, new { message = "Failed to create product", detail = ex.Message });
            }
        }


        [HttpPut("{id:long}")]
        [HasPermission("Product.Manage")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateProductRequest dto)
        {
            var p = await _db.Products
                .Include(x => x.ProductTags).ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);

            if (p == null) return NotFound();

            // Basic validation
            if (dto.Price.HasValue && dto.Price < 0) return BadRequest("Price must be >= 0");
            if (dto.Stock.HasValue && dto.Stock < 0) return BadRequest("Stock must be >= 0");

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Update primitive fields
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

                // -----------------------------
                // TAG UPDATE LOGIC (REPLACE ALL)
                // -----------------------------
                if (dto.Tags != null)
                {
                    // Step 1: Remove ALL existing product-tag links
                    var existingLinks = p.ProductTags.ToList();
                    if (existingLinks.Count > 0)
                    {
                        _db.ProductTags.RemoveRange(existingLinks);
                    }

                    // Step 2: Normalize incoming tag names
                    var incoming = dto.Tags
                        .Select(x => x?.Name?.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (incoming.Count > 0)
                    {
                        var lowerNames = incoming.Select(n => n.ToLowerInvariant()).ToList();

                        // Step 3: Query DB for all matching tags in one call
                        var existingTags = await _db.Tags
                            .Where(t => !t.IsDelete && lowerNames.Contains(t.Name.ToLower()))
                            .ToListAsync();

                        var existingByLower = existingTags.ToDictionary(
                            x => x.Name.ToLowerInvariant(),
                            x => x,
                            StringComparer.OrdinalIgnoreCase
                        );

                        var tagsToAdd = new List<Tag>();

                        // Step 4: Create missing tags
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

                        // Step 5: Bulk insert new tags
                        if (tagsToAdd.Count > 0)
                        {
                            _db.Tags.AddRange(tagsToAdd);
                            await _db.SaveChangesAsync(); // ensures new tag IDs are created
                        }

                        // Step 6: Bulk attach product-tag relationships
                        var productTags = existingByLower.Values
                            .Select(tag => new ProductTag
                            {
                                ProductId = p.Id,
                                TagId = tag.Id
                            })
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
        [HasPermission("Product.Manage")]
        public async Task<IActionResult> Delete(long id)
        {
            var p = await _db.Products.FindAsync(id);
            if (p == null) return NotFound();

            // soft delete
            p.IsDelete = true;
            p.LastModifiedAt = DateTimeOffset.UtcNow;
            p.LastModifiedBy = User.Identity?.Name ?? p.LastModifiedBy;

            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
