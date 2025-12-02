// ITokenService.cs
using System.Collections.Generic;
namespace E_Commerce_Admin_Panel.Services
{
    public interface ITokenService
    {
        string CreateToken(long userId, string userName, IEnumerable<string> permissions, IEnumerable<string> roles);
    }
}
