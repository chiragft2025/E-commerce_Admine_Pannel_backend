using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryAdmin.Domain.Entities
{
    public class Product : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string SKU { get; set; } = null!;
        public string? Description { get; set; }

        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; } = true;

        // FK -> Category
        public long CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        // Product ↔ Tag (many-to-many)
        public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
    }
}
