using BankingSystem.Core.Entities;

namespace BankingSystem.Core.Services
{
    public interface ITransactionService
    {
        Task<Transaction> CreateTransactionAsync(int fromAccountId, int toAccountId, decimal amount, string description, string transactionType);
        Task<Transaction> DepositAsync(int accountId, decimal amount, string description = "Deposit");
        Task<Transaction> WithdrawAsync(int accountId, decimal amount, string description = "Withdrawal");
        Task<Transaction> TransferAsync(int fromAccountId, int toAccountId, decimal amount, string description = "Transfer");
        Task<Transaction?> GetTransactionByIdAsync(int transactionId);
        Task<IEnumerable<Transaction>> GetAccountTransactionsAsync(int accountId, int pageNumber = 1, int pageSize = 50);
        Task<IEnumerable<Transaction>> GetUserTransactionsAsync(string userId, int pageNumber = 1, int pageSize = 50);
        Task<IEnumerable<Transaction>> GetTransactionsByDateRangeAsync(int accountId, DateTime startDate, DateTime endDate);
        Task<decimal> GetAccountTransactionSummaryAsync(int accountId, DateTime? startDate = null, DateTime? endDate = null);
        Task<bool> ReverseTransactionAsync(int transactionId, string reason);
        Task<IEnumerable<Transaction>> GetPendingTransactionsAsync(int accountId);
        Task<bool> ApproveTransactionAsync(int transactionId);
        Task<bool> RejectTransactionAsync(int transactionId, string reason);
    }
}
