using E_Commerce_Admin_Panel.Authorization;
using E_Commerce_Admin_Panel.Dtos.Role;
using InventoryAdmin.Domain.Entities;
using InventoryAdmin.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce_Admin_Panel.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [HasPermission("Role.Manage")]
    public class RoleManagementController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public RoleManagementController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RoleDto>>> GetAll()
        {
            var roles = await _db.Roles
                .AsNoTracking()
                .Where(r => !r.IsDelete)
                .OrderBy(r => r.Name)
                .Select(r => new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    IsActive = r.IsActive
                })
                .ToListAsync();

            return Ok(roles);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<RoleDetailDto>> Get(long id)
        {
            var role = await _db.Roles
                .AsNoTracking()
                .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDelete);

            if (role == null) return NotFound();

            var dto = new RoleDetailDto
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsActive = role.IsActive,
                Permissions = role.RolePermissions
                    .Select(rp => new PermissionDto { Id = rp.PermissionId, Name = rp.Permission?.Name })
                    .ToList()
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleRequest dto)
        {
            if (dto == null) return BadRequest("Payload required");
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name required");

            var name = dto.Name.Trim();
            if (await _db.Roles.AnyAsync(r => r.Name == name && !r.IsDelete))
                return BadRequest(new { message = "Role name already exists" });

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var role = new Role
                {
                    Name = name,
                    Description = dto.Description?.Trim(),
                    IsActive = dto.IsActive ?? true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = User?.Identity?.Name ?? "system"
                };

                _db.Roles.Add(role);
                await _db.SaveChangesAsync();

                if (dto.PermissionIds != null && dto.PermissionIds.Any())
                {
                    var perms = await _db.Permissions
                        .Where(p => dto.PermissionIds.Contains(p.Id) && !p.IsDelete)
                        .ToListAsync();

                    var rolePermissions = perms.Select(p => new RolePermission { RoleId = role.Id, PermissionId = p.Id });
                    _db.RolePermissions.AddRange(rolePermissions);
                    await _db.SaveChangesAsync();
                }

                await tx.CommitAsync();

                var resultDto = new RoleDto { Id = role.Id, Name = role.Name, Description = role.Description, IsActive = role.IsActive };
                return CreatedAtAction(nameof(Get), new { id = role.Id }, resultDto);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { message = "Failed to create role", detail = ex.Message });
            }
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<RoleDto>> Update(long id, [FromBody] UpdateRoleRequest dto)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id && !r.IsDelete);
            if (role == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var newName = dto.Name.Trim();
                if (newName != role.Name && await _db.Roles.AnyAsync(r => r.Name == newName && r.Id != id && !r.IsDelete))
                    return BadRequest(new { message = "Role name already exists" });

                role.Name = newName;
            }

            role.Description = dto.Description ?? role.Description;
            role.IsActive = dto.IsActive ?? role.IsActive;
            role.LastModifiedAt = DateTimeOffset.UtcNow;
            role.LastModifiedBy = User?.Identity?.Name ?? role.LastModifiedBy;

            await _db.SaveChangesAsync();

            var resultDto = new RoleDto { Id = role.Id, Name = role.Name, Description = role.Description, IsActive = role.IsActive };
            return Ok(resultDto);
        }

        [HttpPost("{id:long}/permissions")]
        public async Task<IActionResult> AssignPermissions(long id, [FromBody] AssignPermissionsRequest dto)
        {
            if (dto == null) return BadRequest("Payload required");

            var role = await _db.Roles.Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.Id == id && !r.IsDelete);
            if (role == null) return NotFound();

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var perms = (dto.PermissionIds ?? Enumerable.Empty<long>()).Distinct().ToList();
                var existingPermissions = await _db.Permissions
                    .Where(p => perms.Contains(p.Id) && !p.IsDelete)
                    .Select(p => p.Id)
                    .ToListAsync();

                var toRemove = role.RolePermissions.Where(rp => !existingPermissions.Contains(rp.PermissionId)).ToList();
                if (toRemove.Any()) _db.RolePermissions.RemoveRange(toRemove);

                foreach (var pid in existingPermissions)
                {
                    if (!role.RolePermissions.Any(rp => rp.PermissionId == pid))
                        _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { message = "Failed to assign permissions", detail = ex.Message });
            }
        }

        [HttpGet("{id:long}/permissions")]
        public async Task<ActionResult<IEnumerable<PermissionDto>>> GetPermissions(long id)
        {
            var role = await _db.Roles
                .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDelete);

            if (role == null) return NotFound();

            var permissions = role.RolePermissions
                .Select(rp => new PermissionDto { Id = rp.PermissionId, Name = rp.Permission?.Name })
                .ToList();

            return Ok(permissions);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id && !r.IsDelete);
            if (role == null) return NotFound();

            bool isRoleAssigned = await _db.UserRoles.AnyAsync(ur => ur.RoleId == id);
            if (isRoleAssigned)
            {
                return Conflict(new { message = "Cannot delete this role because it is assigned to one or more users." });
            }

            role.IsDelete = true;
            role.LastModifiedAt = DateTimeOffset.UtcNow;
            role.LastModifiedBy = User?.Identity?.Name ?? role.LastModifiedBy;

            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
