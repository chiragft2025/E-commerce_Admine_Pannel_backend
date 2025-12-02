using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryAdmin.Domain.Entities
{
    public class User : BaseEntity
    {
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;

        public string PasswordHash { get; set; } = null!;
        public bool IsActive { get; set; } = true;

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
