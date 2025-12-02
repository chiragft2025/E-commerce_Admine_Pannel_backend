using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryAdmin.Domain.Entities
{
    public class Tag : BaseEntity
    {
        public string Name { get; set; } = null!;

        // Many-to-many join
        public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
    }
}
