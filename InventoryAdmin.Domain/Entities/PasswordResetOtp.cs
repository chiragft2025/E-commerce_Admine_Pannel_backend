using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryAdmin.Domain.Entities
{
    public class PasswordResetOtp
    {
        public long Id { get; set; }

        public long UserId { get; set; }
        public User User { get; set; } = null!;

        public string OtpHash { get; set; } = null!;
        public DateTimeOffset ExpiresAt { get; set; }
        public bool IsUsed { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
