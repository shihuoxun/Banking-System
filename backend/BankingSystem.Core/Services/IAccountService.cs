using BankingSystem.Core.Entities;

namespace BankingSystem.Core.Services
{
    public interface IAccountService
    {
        Task<Account> CreateAccountAsync(string userId, string accountName, string accountType);
        Task<Account?> GetAccountByIdAsync(int accountId);
        Task<Account?> GetAccountByNumberAsync(string accountNumber);
        Task<IEnumerable<Account>> GetUserAccountsAsync(string userId);
        Task<bool> UpdateAccountAsync(Account account);
        Task<bool> DeactivateAccountAsync(int accountId);
        Task<decimal> GetAccountBalanceAsync(int accountId);
        Task<bool> UpdateBalanceAsync(int accountId, decimal newBalance);
        Task<bool> IsAccountActiveAsync(int accountId);
        Task<bool> CanPerformTransactionAsync(int accountId, decimal amount);
    }
}
