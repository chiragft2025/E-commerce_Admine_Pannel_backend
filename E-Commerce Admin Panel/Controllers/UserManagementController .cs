using E_Commerce_Admin_Panel.Authorization;
using E_Commerce_Admin_Panel.Dtos;
using E_Commerce_Admin_Panel.Dtos.User;
using InventoryAdmin.Domain.Entities;
using InventoryAdmin.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce_Admin_Panel.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserManagementController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly PasswordHasher<User> _passwordHasher;

        public UserManagementController(ApplicationDbContext db)
        {
            _db = db;
            _passwordHasher = new PasswordHasher<User>();
        }

        [HttpGet]
        [HasPermission("User.View")]
        public async Task<ActionResult<PagedResult<UserListItemDto>>> GetAll(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 10;

            var q = _db.Users
                .AsNoTracking()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Where(u => !u.IsDelete);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(p => EF.Functions.Like(p.UserName, $"%{s}%"));
            }

            var total = await q.CountAsync();

            var items = await q
                .OrderBy(u => u.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    IsActive = u.IsActive,
                    Roles = u.UserRoles.Select(ur => ur.Role.Name)
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            var result = new PagedResult<UserListItemDto>
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
        [HasPermission("User.View")]
        public async Task<ActionResult<UserDto>> Get(long id)
        {
            var user = await _db.Users
                .AsNoTracking()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDelete);

            if (user == null) return NotFound();

            var dto = new UserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                IsActive = user.IsActive,
                Roles = user.UserRoles.Select(ur => new RoleBriefDto { RoleId = ur.RoleId, Name = ur.Role?.Name }),
                CreatedBy = user.CreatedBy,
                CreatedAt = user.CreatedAt
            };

            return Ok(dto);
        }

        [HttpPost]
        [HasPermission("User.Create")]
        public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var username = dto.UserName.Trim();
            var email = dto.Email.Trim().ToLowerInvariant();

            if (await _db.Users.AnyAsync(u => u.UserName == username))
                return BadRequest(new { message = "Username already exists" });

            if (await _db.Users.AnyAsync(u => u.Email == email))
                return BadRequest(new { message = "Email already registered" });

            var user = new User
            {
                UserName = username,
                Email = email,
                IsActive = dto.IsActive,
                CreatedBy = User?.Identity?.Name ?? "system",
                CreatedAt = DateTimeOffset.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                // assign role(s) if included, else Viewer
                if (dto.RoleIds != null && dto.RoleIds.Any())
                {
                    var roles = await _db.Roles.Where(r => dto.RoleIds.Contains(r.Id)).ToListAsync();
                    foreach (var r in roles)
                        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = r.Id });
                }
                else
                {
                    var viewerRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Viewer");
                    if (viewerRole != null)
                        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = viewerRole.Id });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // Build response DTO (do not return password/hash)
                var created = await _db.Users
                    .AsNoTracking()
                    .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Id == user.Id);

                var createdDto = new UserDto
                {
                    Id = created!.Id,
                    UserName = created.UserName,
                    Email = created.Email,
                    IsActive = created.IsActive,
                    Roles = created.UserRoles.Select(ur => new RoleBriefDto { RoleId = ur.RoleId, Name = ur.Role?.Name }),
                    CreatedBy = created.CreatedBy,
                    CreatedAt = created.CreatedAt
                };

                return CreatedAtAction(nameof(Get), new { id = createdDto.Id }, createdDto);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                // log exception as needed
                return StatusCode(500, new { message = "Failed to create user", detail = ex.Message });
            }
        }

        [HttpPut("{id:long}")]
        [HasPermission("User.Edit")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateUserRequest dto)
        {
            var user = await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id && !u.IsDelete);
            if (user == null) return NotFound();

            user.UserName = dto.UserName?.Trim() ?? user.UserName;
            user.Email = dto.Email?.Trim().ToLowerInvariant() ?? user.Email;
            user.IsActive = dto.IsActive ?? user.IsActive;
            user.LastModifiedAt = DateTimeOffset.UtcNow;
            user.LastModifiedBy = User.Identity?.Name ?? user.LastModifiedBy;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:long}")]
        [HasPermission("User.Delete")] // fixed permission name
        public async Task<IActionResult> Delete(long id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IsDelete = true;
            user.LastModifiedAt = DateTimeOffset.UtcNow;
            user.LastModifiedBy = User.Identity?.Name ?? user.LastModifiedBy;
            await _db.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id:long}/roles")]
        [HasPermission("User.Edit")]
        public async Task<IActionResult> AssignRoles(long id, [FromBody] AssignRolesRequest dto)
        {
            var user = await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id && !u.IsDelete);
            if (user == null) return NotFound();

            var roleIds = dto.RoleIds?.Distinct().ToList() ?? new List<long>();
            var roles = await _db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync();

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var toRemove = user.UserRoles.Where(ur => !roleIds.Contains(ur.RoleId)).ToList();
                if (toRemove.Any()) _db.UserRoles.RemoveRange(toRemove);

                foreach (var r in roles)
                {
                    if (!user.UserRoles.Any(ur => ur.RoleId == r.Id))
                        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = r.Id });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { message = "Failed to assign roles", detail = ex.Message });
            }
        }

        [HttpGet("{id:long}/roles")]
        [HasPermission("User.View")]
        public async Task<ActionResult<IEnumerable<RoleBriefDto>>> GetUserRoles(long id)
        {
            var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDelete);

            if (user == null) return NotFound();

            var roles = user.UserRoles.Select(ur => new RoleBriefDto { RoleId = ur.RoleId, Name = ur.Role?.Name }).ToList();
            return Ok(roles);
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<ActionResult<UserDto>> Profile()
        {
            var name = User.Identity?.Name;
            if (string.IsNullOrEmpty(name)) return Forbid();

            var user = await _db.Users
                .AsNoTracking()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserName == name && !u.IsDelete);

            if (user == null) return NotFound();

            var dto = new UserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                IsActive = user.IsActive,
                Roles = user.UserRoles.Select(ur => new RoleBriefDto { RoleId = ur.RoleId, Name = ur.Role?.Name }),
                CreatedBy = user.CreatedBy,
                CreatedAt = user.CreatedAt
            };

            return Ok(dto);
        }
    }
}
