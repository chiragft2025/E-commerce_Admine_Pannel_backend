using Microsoft.AspNetCore.Authorization;

namespace E_Commerce_Admin_Panel.Authorization
{
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            // optional role bypass
            if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var has = context.User.HasClaim(c =>
                string.Equals(c.Type, "permission", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Value, requirement.Permission, StringComparison.OrdinalIgnoreCase));

            if (has) context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}
