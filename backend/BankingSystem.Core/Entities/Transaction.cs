using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankingSystem.Core.Entities
{
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string TransactionId { get; set; } = string.Empty;

        public int? FromAccountId { get; set; }
        public int? ToAccountId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(50)]
        public string TransactionType { get; set; } = string.Empty; // Transfer, Deposit, Withdrawal

        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Completed, Failed, Cancelled

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ProcessedAt { get; set; }

        // 外键关联
        [ForeignKey("FromAccountId")]
        public virtual Account? FromAccount { get; set; }

        [ForeignKey("ToAccountId")]
        public virtual Account? ToAccount { get; set; }
    }
}
