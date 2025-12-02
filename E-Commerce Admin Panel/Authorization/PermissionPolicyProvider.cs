using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace E_Commerce_Admin_Panel.Authorization
{
    public class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        const string PREFIX = "Permission:";
        private readonly DefaultAuthorizationPolicyProvider fallback;
        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        {
            fallback = new DefaultAuthorizationPolicyProvider(options);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => fallback.GetDefaultPolicyAsync();
        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => fallback.GetFallbackPolicyAsync();

        public Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
        {
            if (policyName.StartsWith(PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                var permission = policyName.Substring(PREFIX.Length);
                var policy = new AuthorizationPolicyBuilder()
                    .AddRequirements(new PermissionRequirement(permission))
                    .Build();
                return Task.FromResult(policy);
            }
            return fallback.GetPolicyAsync(policyName);
        }
    }
}
