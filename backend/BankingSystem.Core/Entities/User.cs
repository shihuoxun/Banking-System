using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BankingSystem.Core.Entities
{
    public class User : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string IdNumber { get; set; } = string.Empty;

        public DateTime DateOfBirth { get; set; }

        [MaxLength(200)]
        public string Address { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // 失败登录计数
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEndTime { get; set; }
        public DateTime? LastLoginAt { get; set; }

        // 关联账户
        public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();
    }
}