using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryAdmin.Domain.Entities
{
    public class ProductTag
    {
        public long ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public long TagId { get; set; }
        public Tag Tag { get; set; } = null!;
    }
}
