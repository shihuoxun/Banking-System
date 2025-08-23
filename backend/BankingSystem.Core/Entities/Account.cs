using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankingSystem.Core.Entities
{
    public class Account
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string AccountNumber { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string AccountType { get; set; } = string.Empty; // Savings, Checking, etc.

        [Required]
        [MaxLength(100)]
        public string AccountName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AvailableBalance { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // 外键关联
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        // 关联交易
        public virtual ICollection<Transaction> FromTransactions { get; set; } = new List<Transaction>();
        public virtual ICollection<Transaction> ToTransactions { get; set; } = new List<Transaction>();
    }
}