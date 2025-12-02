using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryAdmin.Domain.Entities
{
    public class Category : BaseEntity
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }

        // Category -> Products (1 to many)
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
