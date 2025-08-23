using BankingSystem.Core.Entities;
using BankingSystem.Core.Services;
using BankingSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Data.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly BankingDbContext _context;
        private readonly IAccountService _accountService;
        private readonly ILogger<TransactionService> _logger;

        public TransactionService(
            BankingDbContext context,
            IAccountService accountService,
            ILogger<TransactionService> logger)
        {
            _context = context;
            _accountService = accountService;
            _logger = logger;
        }

        // 修复：移除嵌套事务，让调用者管理事务
        public async Task<Transaction> CreateTransactionAsync(int fromAccountId, int toAccountId, decimal amount, string description, string transactionType)
        {
            try
            {
                var newTransaction = new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),  // 添加这行
                    FromAccountId = fromAccountId == 0 ? null : fromAccountId,  // 修改这行
                    ToAccountId = toAccountId == 0 ? null : toAccountId,        // 修改这行
                    Amount = amount,
                    Description = description,
                    TransactionType = transactionType,
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow  // 添加这行
                };

                _context.Transactions.Add(newTransaction);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Transaction created: {TransactionId} - {TransactionType} of {Amount}",
                    newTransaction.Id, transactionType, amount);

                return newTransaction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transaction from account {FromAccountId} to {ToAccountId}",
                    fromAccountId, toAccountId);
                throw;
            }
        }

        public async Task<Transaction> DepositAsync(int accountId, decimal amount, string description = "Deposit")
        {
            if (amount <= 0)
                throw new ArgumentException("Deposit amount must be positive");

            if (!await _accountService.IsAccountActiveAsync(accountId))
                throw new InvalidOperationException("Account is not active");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 获取当前余额
                var currentBalance = await _accountService.GetAccountBalanceAsync(accountId);
                var newBalance = currentBalance + amount;

                // 更新账户余额
                await _accountService.UpdateBalanceAsync(accountId, newBalance);

                // 创建交易记录（现在不会产生嵌套事务）
                var depositTransaction = await CreateTransactionAsync(0, accountId, amount, description, "Deposit");

                await transaction.CommitAsync();

                _logger.LogInformation("Deposit successful: Account {AccountId}, Amount {Amount}, New Balance {NewBalance}",
                    accountId, amount, newBalance);

                return depositTransaction;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing deposit for account {AccountId}", accountId);
                throw;
            }
        }

        public async Task<Transaction> WithdrawAsync(int accountId, decimal amount, string description = "Withdrawal")
        {
            if (amount <= 0)
                throw new ArgumentException("Withdrawal amount must be positive");

            if (!await _accountService.CanPerformTransactionAsync(accountId, -amount))
                throw new InvalidOperationException("Insufficient funds or account not active");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 获取当前余额
                var currentBalance = await _accountService.GetAccountBalanceAsync(accountId);
                var newBalance = currentBalance - amount;

                // 更新账户余额
                await _accountService.UpdateBalanceAsync(accountId, newBalance);

                // 创建交易记录
                var withdrawalTransaction = await CreateTransactionAsync(accountId, 0, amount, description, "Withdrawal");

                await transaction.CommitAsync();

                _logger.LogInformation("Withdrawal successful: Account {AccountId}, Amount {Amount}, New Balance {NewBalance}",
                    accountId, amount, newBalance);

                return withdrawalTransaction;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing withdrawal for account {AccountId}", accountId);
                throw;
            }
        }

        public async Task<Transaction> TransferAsync(int fromAccountId, int toAccountId, decimal amount, string description = "Transfer")
        {
            if (amount <= 0)
                throw new ArgumentException("Transfer amount must be positive");

            if (fromAccountId == toAccountId)
                throw new ArgumentException("Cannot transfer to the same account");

            if (!await _accountService.CanPerformTransactionAsync(fromAccountId, -amount))
                throw new InvalidOperationException("Insufficient funds or source account not active");

            if (!await _accountService.IsAccountActiveAsync(toAccountId))
                throw new InvalidOperationException("Destination account is not active");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 获取当前余额
                var fromBalance = await _accountService.GetAccountBalanceAsync(fromAccountId);
                var toBalance = await _accountService.GetAccountBalanceAsync(toAccountId);

                // 更新余额
                await _accountService.UpdateBalanceAsync(fromAccountId, fromBalance - amount);
                await _accountService.UpdateBalanceAsync(toAccountId, toBalance + amount);

                // 创建交易记录
                var transferTransaction = await CreateTransactionAsync(fromAccountId, toAccountId, amount, description, "Transfer");

                await transaction.CommitAsync();

                _logger.LogInformation("Transfer successful: From Account {FromAccountId} to Account {ToAccountId}, Amount {Amount}",
                    fromAccountId, toAccountId, amount);

                return transferTransaction;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing transfer from account {FromAccountId} to account {ToAccountId}",
                    fromAccountId, toAccountId);
                throw;
            }
        }

        public async Task<Transaction?> GetTransactionByIdAsync(int transactionId)
        {
            try
            {
                return await _context.Transactions
                    .Include(t => t.FromAccount)
                    .Include(t => t.ToAccount)
                    .FirstOrDefaultAsync(t => t.Id == transactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction by ID {TransactionId}", transactionId);
                throw;
            }
        }

        public async Task<IEnumerable<Transaction>> GetAccountTransactionsAsync(int accountId, int pageNumber = 1, int pageSize = 50)
        {
            try
            {
                return await _context.Transactions
                    .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Include(t => t.FromAccount)
                    .Include(t => t.ToAccount)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions for account {AccountId}", accountId);
                throw;
            }
        }

        public async Task<IEnumerable<Transaction>> GetUserTransactionsAsync(string userId, int pageNumber = 1, int pageSize = 50)
        {
            try
            {
                return await _context.Transactions
                    .Where(t => t.FromAccount!.UserId == userId || t.ToAccount!.UserId == userId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Include(t => t.FromAccount)
                    .Include(t => t.ToAccount)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByDateRangeAsync(int accountId, DateTime startDate, DateTime endDate)
        {
            try
            {
                return await _context.Transactions
                    .Where(t => (t.FromAccountId == accountId || t.ToAccountId == accountId) &&
                               t.CreatedAt >= startDate && t.CreatedAt <= endDate)
                    .OrderByDescending(t => t.CreatedAt)
                    .Include(t => t.FromAccount)
                    .Include(t => t.ToAccount)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions by date range for account {AccountId}", accountId);
                throw;
            }
        }

        public async Task<decimal> GetAccountTransactionSummaryAsync(int accountId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.Transactions
                    .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId);

                if (startDate.HasValue)
                    query = query.Where(t => t.CreatedAt >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(t => t.CreatedAt <= endDate.Value);

                var transactions = await query.ToListAsync();

                decimal total = 0;
                foreach (var transaction in transactions)
                {
                    if (transaction.ToAccountId == accountId)
                        total += transaction.Amount; // 收入
                    else if (transaction.FromAccountId == accountId)
                        total -= transaction.Amount; // 支出
                }

                return total;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating transaction summary for account {AccountId}", accountId);
                throw;
            }
        }

        public async Task<bool> ReverseTransactionAsync(int transactionId, string reason)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var originalTransaction = await GetTransactionByIdAsync(transactionId);
                if (originalTransaction == null || originalTransaction.Status == "Reversed")
                    return false;

                // 创建冲正交易
                var reverseTransaction = new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    FromAccountId = originalTransaction.ToAccountId,
                    ToAccountId = originalTransaction.FromAccountId,
                    Amount = originalTransaction.Amount,
                    Description = $"Reversal: {reason}",
                    TransactionType = "Reversal",
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow
                };

                // 恢复账户余额
                if (originalTransaction.FromAccountId.HasValue && originalTransaction.FromAccountId.Value > 0)
                {
                    var fromBalance = await _accountService.GetAccountBalanceAsync(originalTransaction.FromAccountId.Value);
                    await _accountService.UpdateBalanceAsync(originalTransaction.FromAccountId.Value, fromBalance + originalTransaction.Amount);
                }

                if (originalTransaction.ToAccountId.HasValue && originalTransaction.ToAccountId.Value > 0)
                {
                    var toBalance = await _accountService.GetAccountBalanceAsync(originalTransaction.ToAccountId.Value);
                    await _accountService.UpdateBalanceAsync(originalTransaction.ToAccountId.Value, toBalance - originalTransaction.Amount);
                }

                // 更新原交易状态
                originalTransaction.Status = "Reversed";
                _context.Transactions.Update(originalTransaction);

                // 添加冲正交易
                _context.Transactions.Add(reverseTransaction);

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation("Transaction {TransactionId} reversed successfully", transactionId);
                return true;
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error reversing transaction {TransactionId}", transactionId);
                throw;
            }
        }

        public async Task<IEnumerable<Transaction>> GetPendingTransactionsAsync(int accountId)
        {
            try
            {
                return await _context.Transactions
                    .Where(t => (t.FromAccountId == accountId || t.ToAccountId == accountId) &&
                               t.Status == "Pending")
                    .OrderBy(t => t.CreatedAt)
                    .Include(t => t.FromAccount)
                    .Include(t => t.ToAccount)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending transactions for account {AccountId}", accountId);
                throw;
            }
        }

        public async Task<bool> ApproveTransactionAsync(int transactionId)
        {
            try
            {
                var transaction = await GetTransactionByIdAsync(transactionId);
                if (transaction == null || transaction.Status != "Pending")
                    return false;

                transaction.Status = "Completed";
                _context.Transactions.Update(transaction);

                var result = await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction {TransactionId} approved successfully", transactionId);

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving transaction {TransactionId}", transactionId);
                throw;
            }
        }

        public async Task<bool> RejectTransactionAsync(int transactionId, string reason)
        {
            try
            {
                var transaction = await GetTransactionByIdAsync(transactionId);
                if (transaction == null || transaction.Status != "Pending")
                    return false;

                transaction.Status = "Rejected";
                transaction.Description += $" - Rejected: {reason}";
                _context.Transactions.Update(transaction);

                var result = await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction {TransactionId} rejected: {Reason}", transactionId, reason);

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting transaction {TransactionId}", transactionId);
                throw;
            }
        }
    }
}