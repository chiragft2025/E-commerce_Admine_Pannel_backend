using Microsoft.AspNetCore.Authorization;

namespace E_Commerce_Admin_Panel.Authorization
{
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        const string POLICY_PREFIX = "Permission:";
        public HasPermissionAttribute(string permission)
        {
            Policy = POLICY_PREFIX + permission;
        }
        public static string PolicyName(string permission) => POLICY_PREFIX + permission;
    }
}
