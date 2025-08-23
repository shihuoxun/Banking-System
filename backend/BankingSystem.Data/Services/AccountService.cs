using BankingSystem.Core.Entities;
using BankingSystem.Core.Services;
using BankingSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Data.Services
{
    public class AccountService : IAccountService
    {
        private readonly BankingDbContext _context;
        private readonly ILogger<AccountService> _logger;

        public AccountService(BankingDbContext context, ILogger<AccountService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Account> CreateAccountAsync(string userId, string accountName, string accountType)
        {
            try
            {
                var account = new Account
                {
                    UserId = userId,
                    AccountName = accountName,
                    AccountType = accountType,
                    AccountNumber = GenerateAccountNumber(),
                    Balance = 0,
                    AvailableBalance = 0, // ✅ 修复：添加 AvailableBalance 初始化
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Account created successfully for user {UserId} with account number {AccountNumber}",
                    userId, account.AccountNumber);

                return account;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account for user {UserId}", userId);
                throw;
            }
        }

        public async Task<Account?> GetAccountByIdAsync(int accountId)
        {
            try
            {
                return await _context.Accounts
                    .Include(a => a.User)
                    .Include(a => a.FromTransactions) // ✅ 修复：包含交易关联
                    .Include(a => a.ToTransactions)   // ✅ 修复：包含交易关联
                    .FirstOrDefaultAsync(a => a.Id == accountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account by ID {AccountId}", accountId);
                throw;
            }
        }

        public async Task<Account?> GetAccountByNumberAsync(string accountNumber)
        {
            try
            {
                return await _context.Accounts
                    .Include(a => a.User)
                    .Include(a => a.FromTransactions) // ✅ 修复：包含交易关联
                    .Include(a => a.ToTransactions)   // ✅ 修复：包含交易关联
                    .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account by number {AccountNumber}", accountNumber);
                throw;
            }
        }

        public async Task<IEnumerable<Account>> GetUserAccountsAsync(string userId)
        {
            try
            {
                return await _context.Accounts
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateAccountAsync(Account account)
        {
            try
            {
                account.UpdatedAt = DateTime.UtcNow;
                _context.Accounts.Update(account);
                var result = await _context.SaveChangesAsync();

                _logger.LogInformation("Account {AccountId} updated successfully", account.Id);
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account {AccountId}", account.Id);
                throw;
            }
        }

        public async Task<bool> DeactivateAccountAsync(int accountId)
        {
            try
            {
                var account = await GetAccountByIdAsync(accountId);
                if (account == null)
                    return false;

                account.IsActive = false;
                account.UpdatedAt = DateTime.UtcNow;

                var result = await _context.SaveChangesAsync();
                _logger.LogInformation("Account {AccountId} deactivated successfully", accountId);

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating account {AccountId}", accountId);
                throw;
            }
        }

        public async Task<decimal> GetAccountBalanceAsync(int accountId)
        {
            try
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId);

                return account?.Balance ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting balance for account {AccountId}", accountId);
                throw;
            }
        }

        public async Task<bool> UpdateBalanceAsync(int accountId, decimal newBalance)
        {
            try
            {
                var account = await GetAccountByIdAsync(accountId);
                if (account == null)
                    return false;

                account.Balance = newBalance;
                account.AvailableBalance = newBalance; // ✅ 修复：同时更新 AvailableBalance
                account.UpdatedAt = DateTime.UtcNow;

                var result = await _context.SaveChangesAsync();
                _logger.LogInformation("Balance updated for account {AccountId} to {NewBalance}",
                    accountId, newBalance);

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating balance for account {AccountId}", accountId);
                throw;
            }
        }

        public async Task<bool> IsAccountActiveAsync(int accountId)
        {
            try
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId);

                return account?.IsActive ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if account {AccountId} is active", accountId);
                throw;
            }
        }

        public async Task<bool> CanPerformTransactionAsync(int accountId, decimal amount)
        {
            try
            {
                var account = await GetAccountByIdAsync(accountId);
                if (account == null || !account.IsActive)
                    return false;

                // ✅ 修复：使用 AvailableBalance 而不是 Balance 进行交易检查
                // 检查余额是否足够（对于支出交易）
                if (amount < 0 && account.AvailableBalance < Math.Abs(amount))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking transaction capability for account {AccountId}", accountId);
                throw;
            }
        }

        private string GenerateAccountNumber()
        {
            // 生成10位账户号码：银行代码(3位) + 时间戳(6位) + 随机数(1位)
            var bankCode = "001"; // 银行代码
            var timestamp = DateTime.UtcNow.ToString("MMddHH"); // 月日小时
            var random = new Random().Next(0, 10); // 0-9随机数

            return $"{bankCode}{timestamp}{random}";
        }

        // ✅ 新增：专门用于交易的余额更新方法
        public async Task<bool> UpdateBalancesAsync(int accountId, decimal newBalance, decimal newAvailableBalance)
        {
            try
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId);
                
                if (account == null)
                    return false;

                account.Balance = newBalance;
                account.AvailableBalance = newAvailableBalance;
                account.UpdatedAt = DateTime.UtcNow;

                var result = await _context.SaveChangesAsync();
                _logger.LogInformation("Balances updated for account {AccountId}: Balance={Balance}, Available={Available}",
                    accountId, newBalance, newAvailableBalance);

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating balances for account {AccountId}", accountId);
                throw;
            }
        }
    }
}