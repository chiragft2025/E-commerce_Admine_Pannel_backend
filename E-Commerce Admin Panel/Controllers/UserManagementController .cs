using E_Commerce_Admin_Panel.Authorization;
using E_Commerce_Admin_Panel.Dtos.User;
using InventoryAdmin.Domain.Entities;
using InventoryAdmin.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 10;

            var q = _db.Users
                .AsNoTracking()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Where(u => !u.IsDelete);
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(p => p.UserName.Contains(search));


            var total = await q.CountAsync();
            var items = await q
                .OrderBy(u => u.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.IsActive,
                    Roles = u.UserRoles.Select(ur => ur.Role.Name)
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        
        // GET: api/UserManagement/{id}
        [HttpGet("{id:long}")]
        [HasPermission("User.View")]
        public async Task<IActionResult> Get(long id)
        {

            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDelete);

            if (user == null) return NotFound();

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.IsActive,
                Roles = user.UserRoles.Select(ur => new { ur.RoleId, ur.Role.Name })
            });
        }

        
        [HttpPost]
        [HasPermission("User.Create")]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Normalize
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
                CreatedBy = User.Identity?.Name ?? "system",
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
                    foreach (var rid in dto.RoleIds.Distinct())
                    {
                        var role = await _db.Roles.FindAsync(rid);
                        if (role != null)
                            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
                    }
                }
                else
                {
                    var viewerRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Viewer");
                    if (viewerRole != null)
                        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = viewerRole.Id });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return CreatedAtAction(nameof(Get), new { id = user.Id }, new { user.Id, user.UserName, user.Email });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { message = "Failed to create user", detail = ex.Message });
            }
        }

        // PUT: api/UserManagement/{id}
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
            return Ok();
        }

        // DELETE: api/UserManagement/{id} (soft delete)
        [HttpDelete("{id:long}")]
        [HasPermission("User.Delte")]
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

        // POST: api/UserManagement/{id}/roles  (assign roles)
        [HttpPost("{id:long}/roles")]
        [HasPermission("User.Edit")]
        public async Task<IActionResult> AssignRoles(long id, [FromBody] AssignRolesRequest dto)
        {
           

            var user = await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id && !u.IsDelete);
            if (user == null) return NotFound();

            // Validate roles
            var roles = await _db.Roles.Where(r => dto.RoleIds.Contains(r.Id)).ToListAsync();

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Remove existing roles that are not in the new list
                var toRemove = user.UserRoles.Where(ur => !dto.RoleIds.Contains(ur.RoleId)).ToList();
                if (toRemove.Any())
                {
                    _db.UserRoles.RemoveRange(toRemove);
                }

                // Add missing roles
                foreach (var r in roles)
                {
                    if (!user.UserRoles.Any(ur => ur.RoleId == r.Id))
                    {
                        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = r.Id });
                    }
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

        // GET: api/UserManagement/{id}/roles
        [HttpGet("{id:long}/roles")]
        public async Task<IActionResult> GetUserRoles(long id)
        {
            var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDelete);

            if (user == null) return NotFound();

            return Ok(user.UserRoles.Select(ur => new { ur.RoleId, ur.Role.Name }));
        }

    }
}

