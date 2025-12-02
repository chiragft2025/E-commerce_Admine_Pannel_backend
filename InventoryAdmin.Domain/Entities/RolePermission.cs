using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryAdmin.Domain.Entities
{
    public class RolePermission
    {
        public long RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public long PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;
    }
}
