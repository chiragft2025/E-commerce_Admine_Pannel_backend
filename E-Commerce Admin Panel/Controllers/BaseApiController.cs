using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace E_Commerce_Admin_Panel.Controllers
{
    [ApiController]
    public abstract class BaseApiController : ControllerBase
    {
        protected string? GetCurrentUsername()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return null;

            return User.Identity!.Name
                ?? User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst("preferred_username")?.Value
                ?? User.FindFirst("username")?.Value
                ?? null;
        }

        protected bool IsAdmin()
        {
            if (User == null) return false;

            if (User.IsInRole("Admin")) return true;

            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value)
                        .Concat(User.FindAll("role").Select(c => c.Value));

            return roles.Any(r =>
                string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)
            );
        }
    }
}
