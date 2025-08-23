using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BankingSystem.Core.Entities;
using BankingSystem.Core.Services;
using BankingSystem.Data.Services;

namespace BankingSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IAccountService accountService, ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _logger = logger;
        }

        /// <summary>
        /// 创建新的银行账户
        /// </summary>
        /// <param name="request">账户创建请求</param>
        /// <returns>创建的账户信息</returns>
        [HttpPost("create")]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Creating account for user {UserId}", userId);

                // 验证账户类型
                var validAccountTypes = new[] { "Savings", "Checking", "Business", "Investment" };
                if (!validAccountTypes.Contains(request.AccountType))
                {
                    return BadRequest(new { message = "Invalid account type. Valid types: Savings, Checking, Business, Investment" });
                }

                // 验证初始存款
                if (request.InitialDeposit < 0)
                {
                    return BadRequest(new { message = "Initial deposit cannot be negative" });
                }

                var createdAccount = await _accountService.CreateAccountAsync(userId, request.AccountName, request.AccountType);

                // 如果有初始存款，更新余额
                if (request.InitialDeposit > 0)
                {
                    await _accountService.UpdateBalanceAsync(createdAccount.Id, request.InitialDeposit);
                    // 重新获取更新后的账户信息，添加空检查
                    var updatedAccount = await _accountService.GetAccountByIdAsync(createdAccount.Id);
                    if (updatedAccount != null)
                    {
                        createdAccount = updatedAccount;
                    }
                }

                _logger.LogInformation("Account created successfully with ID {AccountId}", createdAccount.Id);

                var response = new AccountResponse
                {
                    Id = createdAccount.Id,
                    AccountNumber = createdAccount.AccountNumber,
                    AccountName = createdAccount.AccountName,
                    AccountType = createdAccount.AccountType,
                    Balance = createdAccount.Balance,
                    AvailableBalance = createdAccount.AvailableBalance,
                    IsActive = createdAccount.IsActive,
                    CreatedAt = createdAccount.CreatedAt
                };

                return CreatedAtAction(nameof(GetAccount), new { id = createdAccount.Id }, response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid account creation request: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account for user {UserId}", GetCurrentUserId());
                return StatusCode(500, new { message = "An error occurred while creating the account" });
            }
        }

        /// <summary>
        /// 获取当前用户的所有银行账户
        /// </summary>
        /// <returns>用户的银行账户列表</returns>
        [HttpGet("list")]
        public async Task<IActionResult> GetUserAccounts()
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Retrieving accounts for user {UserId}", userId);

                var accounts = await _accountService.GetUserAccountsAsync(userId);

                var response = accounts.Select(account => new AccountResponse
                {
                    Id = account.Id,
                    AccountNumber = account.AccountNumber,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType,
                    Balance = account.Balance,
                    AvailableBalance = account.AvailableBalance,
                    IsActive = account.IsActive,
                    CreatedAt = account.CreatedAt
                }).ToList();

                return Ok(new { accounts = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving accounts for user {UserId}", GetCurrentUserId());
                return StatusCode(500, new { message = "An error occurred while retrieving accounts" });
            }
        }

        /// <summary>
        /// 获取特定账户的详细信息
        /// </summary>
        /// <param name="id">账户ID</param>
        /// <returns>账户详细信息</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAccount(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Retrieving account {AccountId} for user {UserId}", id, userId);

                var account = await _accountService.GetAccountByIdAsync(id);

                if (account == null)
                {
                    _logger.LogWarning("Account {AccountId} not found", id);
                    return NotFound(new { message = "Account not found" });
                }

                // 验证账户所有权
                if (account.UserId != userId && !IsAdmin())
                {
                    _logger.LogWarning("User {UserId} attempted to access account {AccountId} belonging to user {AccountOwnerId}",
                        userId, id, account.UserId);
                    return Forbid();
                }

                var response = new AccountDetailResponse
                {
                    Id = account.Id,
                    AccountNumber = account.AccountNumber,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType,
                    Balance = account.Balance,
                    AvailableBalance = account.AvailableBalance,
                    IsActive = account.IsActive,
                    CreatedAt = account.CreatedAt,
                    UpdatedAt = account.UpdatedAt,
                    LastTransactionDate = GetLastTransactionDate(account)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving account {AccountId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the account" });
            }
        }

        /// <summary>
        /// 更新账户信息
        /// </summary>
        /// <param name="id">账户ID</param>
        /// <param name="request">更新请求</param>
        /// <returns>更新结果</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAccount(int id, [FromBody] UpdateAccountRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Updating account {AccountId} for user {UserId}", id, userId);

                var account = await _accountService.GetAccountByIdAsync(id);

                if (account == null)
                {
                    return NotFound(new { message = "Account not found" });
                }

                // 验证账户所有权
                if (account.UserId != userId && !IsAdmin())
                {
                    return Forbid();
                }

                // 更新允许修改的字段
                if (!string.IsNullOrWhiteSpace(request.AccountName))
                {
                    account.AccountName = request.AccountName;
                    account.UpdatedAt = DateTime.UtcNow;
                }

                var updateResult = await _accountService.UpdateAccountAsync(account);

                if (!updateResult)
                {
                    return StatusCode(500, new { message = "Failed to update account" });
                }

                // 重新获取更新后的账户
                var updatedAccount = await _accountService.GetAccountByIdAsync(id);
                if (updatedAccount == null)
                {
                    return StatusCode(500, new { message = "Failed to retrieve updated account" });
                }

                var response = new AccountResponse
                {
                    Id = updatedAccount.Id,
                    AccountNumber = updatedAccount.AccountNumber,
                    AccountName = updatedAccount.AccountName,
                    AccountType = updatedAccount.AccountType,
                    Balance = updatedAccount.Balance,
                    AvailableBalance = updatedAccount.AvailableBalance,
                    IsActive = updatedAccount.IsActive,
                    CreatedAt = updatedAccount.CreatedAt
                };

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid account update request: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account {AccountId}", id);
                return StatusCode(500, new { message = "An error occurred while updating the account" });
            }
        }

        /// <summary>
        /// 冻结账户（停用账户）
        /// </summary>
        /// <param name="id">账户ID</param>
        /// <returns>操作结果</returns>
        [HttpPost("{id}/freeze")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> FreezeAccount(int id)
        {
            try
            {
                _logger.LogInformation("Freezing account {AccountId}", id);

                var account = await _accountService.GetAccountByIdAsync(id);

                if (account == null)
                {
                    return NotFound(new { message = "Account not found" });
                }

                if (!account.IsActive)
                {
                    return BadRequest(new { message = "Account is already frozen" });
                }

                var result = await _accountService.DeactivateAccountAsync(id);

                if (!result)
                {
                    return StatusCode(500, new { message = "Failed to freeze account" });
                }

                _logger.LogInformation("Account {AccountId} has been frozen", id);

                return Ok(new { message = "Account has been frozen successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error freezing account {AccountId}", id);
                return StatusCode(500, new { message = "An error occurred while freezing the account" });
            }
        }

        /// <summary>
        /// 解冻账户（重新激活账户）
        /// </summary>
        /// <param name="id">账户ID</param>
        /// <returns>操作结果</returns>
        [HttpPost("{id}/unfreeze")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnfreezeAccount(int id)
        {
            try
            {
                _logger.LogInformation("Unfreezing account {AccountId}", id);

                var account = await _accountService.GetAccountByIdAsync(id);

                if (account == null)
                {
                    return NotFound(new { message = "Account not found" });
                }

                if (account.IsActive)
                {
                    return BadRequest(new { message = "Account is not frozen" });
                }

                // 重新激活账户
                account.IsActive = true;
                account.UpdatedAt = DateTime.UtcNow;

                var result = await _accountService.UpdateAccountAsync(account);

                if (!result)
                {
                    return StatusCode(500, new { message = "Failed to unfreeze account" });
                }

                _logger.LogInformation("Account {AccountId} has been unfrozen", id);

                return Ok(new { message = "Account has been unfrozen successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unfreezing account {AccountId}", id);
                return StatusCode(500, new { message = "An error occurred while unfreezing the account" });
            }
        }

        /// <summary>
        /// 获取账户余额
        /// </summary>
        /// <param name="id">账户ID</param>
        /// <returns>账户余额</returns>
        [HttpGet("{id}/balance")]
        public async Task<IActionResult> GetAccountBalance(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var account = await _accountService.GetAccountByIdAsync(id);

                if (account == null)
                {
                    return NotFound(new { message = "Account not found" });
                }

                // 验证账户所有权
                if (account.UserId != userId && !IsAdmin())
                {
                    return Forbid();
                }

                var balance = await _accountService.GetAccountBalanceAsync(id);

                return Ok(new
                {
                    accountId = account.Id,
                    accountNumber = account.AccountNumber,
                    balance = balance,
                    availableBalance = account.AvailableBalance,
                    isActive = account.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving balance for account {AccountId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the account balance" });
            }
        }

        /// <summary>
        /// 检查账户是否可以执行交易
        /// </summary>
        /// <param name="id">账户ID</param>
        /// <param name="amount">交易金额</param>
        /// <returns>是否可以执行交易</returns>
        [HttpGet("{id}/can-transact")]
        public async Task<IActionResult> CanPerformTransaction(int id, [FromQuery] decimal amount)
        {
            try
            {
                var userId = GetCurrentUserId();
                var account = await _accountService.GetAccountByIdAsync(id);

                if (account == null)
                {
                    return NotFound(new { message = "Account not found" });
                }

                // 验证账户所有权
                if (account.UserId != userId && !IsAdmin())
                {
                    return Forbid();
                }

                var canTransact = await _accountService.CanPerformTransactionAsync(id, amount);
                var isActive = await _accountService.IsAccountActiveAsync(id);

                return Ok(new
                {
                    accountId = id,
                    canPerformTransaction = canTransact,
                    isActive = isActive,
                    requestedAmount = amount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking transaction capability for account {AccountId}", id);
                return StatusCode(500, new { message = "An error occurred while checking transaction capability" });
            }
        }

        // 辅助方法
        private string GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("Invalid user ID in token");
            }
            return userIdClaim;
        }

        private bool IsAdmin()
        {
            return User.IsInRole("Admin");
        }

        // 安全的最后交易时间获取方法
        private static DateTime? GetLastTransactionDate(Account account)
        {
            var fromTransactionDate = account.FromTransactions?
                .Where(t => t != null)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault()?.CreatedAt;

            var toTransactionDate = account.ToTransactions?
                .Where(t => t != null)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault()?.CreatedAt;

            if (fromTransactionDate.HasValue && toTransactionDate.HasValue)
                return fromTransactionDate > toTransactionDate ? fromTransactionDate : toTransactionDate;

            return fromTransactionDate ?? toTransactionDate;
        }
    }

    // DTO类定义
    public class CreateAccountRequest
    {
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty; // "Savings", "Checking", "Business", "Investment"
        public decimal InitialDeposit { get; set; } = 0;
    }

    public class UpdateAccountRequest
    {
        public string? AccountName { get; set; }
    }

    public class AccountResponse
    {
        public int Id { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public decimal AvailableBalance { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AccountDetailResponse : AccountResponse
    {
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastTransactionDate { get; set; }
    }
}