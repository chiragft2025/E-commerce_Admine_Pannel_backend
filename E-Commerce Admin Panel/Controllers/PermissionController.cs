using InventoryAdmin.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce_Admin_Panel.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]  // adjust if needed
    public class PermissionsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public PermissionsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: api/Permissions
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var permissions = await _db.Permissions
                .AsNoTracking()
                .Where(p => !p.IsDelete)
                .OrderBy(p => p.Name)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description
                })
                .ToListAsync();

            return Ok(permissions);
        }
    }
}
