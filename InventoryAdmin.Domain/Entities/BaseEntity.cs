using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryAdmin.Domain.Entities
{
    public abstract class BaseEntity
    {
        [Key]
        public long Id { get; set; }

        public required string CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public string? LastModifiedBy { get; set; }
        public DateTimeOffset? LastModifiedAt { get; set; }

        public bool IsDelete { get; set; } = false;
    }
}
