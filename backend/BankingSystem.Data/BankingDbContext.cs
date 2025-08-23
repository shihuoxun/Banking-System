using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BankingSystem.Core.Entities;

namespace BankingSystem.Data
{
    public class BankingDbContext : IdentityDbContext<User>
    {
        public BankingDbContext(DbContextOptions<BankingDbContext> options) : base(options)
        {
        }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 账户配置
            builder.Entity<Account>(entity =>
            {
                entity.HasIndex(e => e.AccountNumber).IsUnique();
                entity.Property(e => e.Balance).HasPrecision(18, 2);
                entity.Property(e => e.AvailableBalance).HasPrecision(18, 2);

                entity.HasOne(e => e.User)
                      .WithMany(u => u.Accounts)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 交易配置
            builder.Entity<Transaction>(entity =>
            {
                entity.HasIndex(e => e.TransactionId).IsUnique();
                entity.Property(e => e.Amount).HasPrecision(18, 2);

                entity.HasOne(e => e.FromAccount)
                      .WithMany(a => a.FromTransactions)
                      .HasForeignKey(e => e.FromAccountId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ToAccount)
                      .WithMany(a => a.ToTransactions)
                      .HasForeignKey(e => e.ToAccountId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 用户配置
            builder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.IdNumber).IsUnique();
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            });
        }
    }
}
