using E_Commerce_Admin_Panel.Dtos.Auth;
using E_Commerce_Admin_Panel.Services;
using InventoryAdmin.Domain.Entities;
using InventoryAdmin.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Reflection.Metadata.Ecma335;

namespace E_Commerce_Admin_Panel.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ITokenService _tokenService;
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthController(ApplicationDbContext db, ITokenService tokenService)
        {
            _db = db;
            _tokenService = tokenService;
            _passwordHasher = new PasswordHasher<User>();
        }

          // Creates a user and assigns the "Viewer" role by default
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // normalize
            var uname = req.UserName.Trim();
            var email = req.Email.Trim().ToLowerInvariant();

            // check duplicates
            if (await _db.Users.AnyAsync(u => u.UserName == uname))
                return BadRequest(new { message = "Username already taken" });

            if (await _db.Users.AnyAsync(u => u.Email == email))
                return BadRequest(new { message = "Email already registered" });

            var user = new User
            {
                UserName = uname,
                Email = email,
                IsActive = true,
                CreatedBy = "self-register",
                CreatedAt = DateTimeOffset.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, req.Password);

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                // assign default role (Viewer)
                var viewerRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Viewer");
                if (viewerRole != null)
                {
                    _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = viewerRole.Id });
                    await _db.SaveChangesAsync();
                }

                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { message = "Failed to create user", detail = ex.Message });
            }

            return CreatedAtAction(nameof(Register), new { userName = user.UserName }, new { user.Id, user.UserName, user.Email });
        }

        // --------------------- Login ---------------------
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("username and password are required");

            // find the user (case-sensitive username check + only active and not soft-deleted)
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u =>
                    EF.Functions.Collate(u.UserName, "SQL_Latin1_General_CP1_CS_AS") == req.UserName
                    && !u.IsDelete
                    && u.IsActive
                );

            if (user == null)
                return BadRequest("invalid credentials");

            // verify password
            var pwResult = new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, req.Password);
            if (pwResult == PasswordVerificationResult.Failed)
                return BadRequest("invalid credentials");

            // gather role ids
            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();

            // gather permissions
            var permissions = await _db.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Include(rp => rp.Permission)
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToListAsync();

            // gather role names
            var roles = user.UserRoles
                .Select(ur => ur.Role?.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();

            // create JWT token
            var token = _tokenService.CreateToken(user.Id, user.UserName, permissions, roles);

            return Ok(new
            {
                Token = token,
                UserName = user.UserName,
                Roles = roles,
                Permissions = permissions
            });
        }

        // --------------------- Me (test protected endpoint) ---------------------
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            var username = User.Identity?.Name ?? User.Claims.FirstOrDefault(c => c.Type == "username")?.Value;
            var permissions = User.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();
            return Ok(new { user = username, permissions });
        }
    }
}
