using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BankingSystem.Core.Entities;
using BankingSystem.Core.Services;

namespace BankingSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly IAccountService _accountService;
        private readonly ILogger<TransactionController> _logger;

        public TransactionController(
            ITransactionService transactionService,
            IAccountService accountService,
            ILogger<TransactionController> logger)
        {
            _transactionService = transactionService;
            _accountService = accountService;
            _logger = logger;
        }

        /// <summary>
        /// 存款
        /// </summary>
        /// <param name="request">存款请求</param>
        /// <returns>交易结果</returns>
        [HttpPost("deposit")]
        public async Task<IActionResult> Deposit([FromBody] DepositRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Processing deposit for account {AccountId} by user {UserId}",
                    request.AccountId, userId);

                // 验证账户所有权
                var account = await _accountService.GetAccountByIdAsync(request.AccountId);
                if (account == null)
                {
                    return NotFound(new { message = "Account not found" });
                }

                if (account.UserId != userId && !IsAdmin())
                {
                    return Forbid();
                }

                // 验证账户状态
                if (!account.IsActive)
                {
                    return BadRequest(new { message = "Account is not active" });
                }

                // 验证金额
                if (request.Amount <= 0)
                {
                    return BadRequest(new { message = "Amount must be greater than zero" });
                }

                var transaction = await _transactionService.DepositAsync(
                    request.AccountId,
                    request.Amount,
                    request.Description ?? "Deposit");

                _logger.LogInformation("Deposit successful: Transaction {TransactionId}", transaction.Id);

                var response = MapToTransactionResponse(transaction);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid deposit request: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing deposit for account {AccountId}", request.AccountId);
                // 临时返回详细错误信息用于调试
                return StatusCode(500, new { 
                    message = "An error occurred while processing the deposit", 
                    error = ex.Message, 
                    innerError = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace?.Split('\n').Take(5).ToArray() // 只取前5行堆栈信息
                });
            }
        }

        /// <summary>
        /// 取款
        /// </summary>
        /// <param name="request">取款请求</param>
        /// <returns>交易结果</returns>
        [HttpPost("withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] WithdrawRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Processing withdrawal for account {AccountId} by user {UserId}",
                    request.AccountId, userId);

                // 验证账户所有权
                var account = await _accountService.GetAccountByIdAsync(request.AccountId);
                if (account == null)
                {
                    return NotFound(new { message = "Account not found" });
                }

                if (account.UserId != userId && !IsAdmin())
                {
                    return Forbid();
                }

                // 验证账户状态
                if (!account.IsActive)
                {
                    return BadRequest(new { message = "Account is not active" });
                }

                // 验证金额
                if (request.Amount <= 0)
                {
                    return BadRequest(new { message = "Amount must be greater than zero" });
                }

                // 检查余额是否足够
                if (!await _accountService.CanPerformTransactionAsync(request.AccountId, -request.Amount))
                {
                    return BadRequest(new { message = "Insufficient funds" });
                }

                var transaction = await _transactionService.WithdrawAsync(
                    request.AccountId,
                    request.Amount,
                    request.Description ?? "Withdrawal");

                _logger.LogInformation("Withdrawal successful: Transaction {TransactionId}", transaction.Id);

                var response = MapToTransactionResponse(transaction);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid withdrawal request: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing withdrawal for account {AccountId}", request.AccountId);
                // 临时返回详细错误信息用于调试
                return StatusCode(500, new { 
                    message = "An error occurred while processing the withdrawal", 
                    error = ex.Message, 
                    innerError = ex.InnerException?.Message 
                });
            }
        }

        /// <summary>
        /// 转账
        /// </summary>
        /// <param name="request">转账请求</param>
        /// <returns>交易结果</returns>
        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Processing transfer from account {FromAccountId} to {ToAccountId} by user {UserId}",
                    request.FromAccountId, request.ToAccountId, userId);

                // 验证源账户所有权
                var fromAccount = await _accountService.GetAccountByIdAsync(request.FromAccountId);
                if (fromAccount == null)
                {
                    return NotFound(new { message = "Source account not found" });
                }

                if (fromAccount.UserId != userId && !IsAdmin())
                {
                    return Forbid();
                }

                // 验证目标账户存在
                var toAccount = await _accountService.GetAccountByIdAsync(request.ToAccountId);
                if (toAccount == null)
                {
                    return NotFound(new { message = "Destination account not found" });
                }

                // 验证账户状态
                if (!fromAccount.IsActive)
                {
                    return BadRequest(new { message = "Source account is not active" });
                }

                if (!toAccount.IsActive)
                {
                    return BadRequest(new { message = "Destination account is not active" });
                }

                // 验证不能向自己转账
                if (request.FromAccountId == request.ToAccountId)
                {
                    return BadRequest(new { message = "Cannot transfer to the same account" });
                }

                // 验证金额
                if (request.Amount <= 0)
                {
                    return BadRequest(new { message = "Amount must be greater than zero" });
                }

                // 检查余额是否足够
                if (!await _accountService.CanPerformTransactionAsync(request.FromAccountId, -request.Amount))
                {
                    return BadRequest(new { message = "Insufficient funds" });
                }

                var transaction = await _transactionService.TransferAsync(
                    request.FromAccountId,
                    request.ToAccountId,
                    request.Amount,
                    request.Description ?? "Transfer");

                _logger.LogInformation("Transfer successful: Transaction {TransactionId}", transaction.Id);

                var response = MapToTransactionResponse(transaction);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid transfer request: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing transfer from account {FromAccountId} to {ToAccountId}",
                    request.FromAccountId, request.ToAccountId);
                // 临时返回详细错误信息用于调试
                return StatusCode(500, new { 
                    message = "An error occurred while processing the transfer", 
                    error = ex.Message, 
                    innerError = ex.InnerException?.Message 
                });
            }
        }

        /// <summary>
        /// 获取账户交易历史
        /// </summary>
        /// <param name="accountId">账户ID</param>
        /// <param name="pageNumber">页码</param>
        /// <param name="pageSize">页大小</param>
        /// <returns>交易历史列表</returns>
        [HttpGet("history/{accountId}")]
        public async Task<IActionResult> GetTransactionHistory(
            int accountId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Retrieving transaction history for account {AccountId} by user {UserId}",
                    accountId, userId);

                // 验证账户所有权
                var account = await _accountService.GetAccountByIdAsync(accountId);
                if (account == null)
                {
                    return NotFound(new { message = "Account not found" });
                }

                if (account.UserId != userId && !IsAdmin())
                {
                    return Forbid();
                }

                // 验证分页参数
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 50;

                var transactions = await _transactionService.GetAccountTransactionsAsync(
                    accountId, pageNumber, pageSize);

                var response = transactions.Select(MapToTransactionResponse).ToList();

                return Ok(new
                {
                    accountId = accountId,
                    pageNumber = pageNumber,
                    pageSize = pageSize,
                    transactions = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transaction history for account {AccountId}", accountId);
                return StatusCode(500, new { message = "An error occurred while retrieving transaction history" });
            }
        }

        /// <summary>
        /// 获取用户所有交易历史
        /// </summary>
        /// <param name="pageNumber">页码</param>
        /// <param name="pageSize">页大小</param>
        /// <returns>用户所有交易历史</returns>
        [HttpGet("user-history")]
        public async Task<IActionResult> GetUserTransactionHistory(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Retrieving all transaction history for user {UserId}", userId);

                // 验证分页参数
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 50;

                var transactions = await _transactionService.GetUserTransactionsAsync(
                    userId, pageNumber, pageSize);

                var response = transactions.Select(MapToTransactionResponse).ToList();

                return Ok(new
                {
                    userId = userId,
                    pageNumber = pageNumber,
                    pageSize = pageSize,
                    transactions = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user transaction history for user {UserId}", GetCurrentUserId());
                return StatusCode(500, new { message = "An error occurred while retrieving transaction history" });
            }
        }

        /// <summary>
        /// 获取交易详情
        /// </summary>
        /// <param name="id">交易ID</param>
        /// <returns>交易详情</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTransactionDetails(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Retrieving transaction details for transaction {TransactionId} by user {UserId}",
                    id, userId);

                var transaction = await _transactionService.GetTransactionByIdAsync(id);

                if (transaction == null)
                {
                    return NotFound(new { message = "Transaction not found" });
                }

                // 验证用户权限 - 用户只能查看自己账户相关的交易
                var canAccess = false;

                if (IsAdmin())
                {
                    canAccess = true;
                }
                else
                {
                    // 检查FromAccount所有权
                    if (transaction.FromAccountId.HasValue)
                    {
                        var fromAccount = await _accountService.GetAccountByIdAsync(transaction.FromAccountId.Value);
                        if (fromAccount?.UserId == userId)
                        {
                            canAccess = true;
                        }
                    }

                    // 检查ToAccount所有权
                    if (!canAccess && transaction.ToAccountId.HasValue)
                    {
                        var toAccount = await _accountService.GetAccountByIdAsync(transaction.ToAccountId.Value);
                        if (toAccount?.UserId == userId)
                        {
                            canAccess = true;
                        }
                    }
                }

                if (!canAccess)
                {
                    return Forbid();
                }

                var response = MapToTransactionDetailResponse(transaction);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transaction details for transaction {TransactionId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving transaction details" });
            }
        }

        /// <summary>
        /// 获取账户交易汇总
        /// </summary>
        /// <param name="accountId">账户ID</param>
        /// <param name="startDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <returns>交易汇总</returns>
        [HttpGet("summary/{accountId}")]
        public async Task<IActionResult> GetTransactionSummary(
            int accountId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Retrieving transaction summary for account {AccountId} by user {UserId}",
                    accountId, userId);

                // 验证账户所有权
                var account = await _accountService.GetAccountByIdAsync(accountId);
                if (account == null)
                {
                    return NotFound(new { message = "Account not found" });
                }

                if (account.UserId != userId && !IsAdmin())
                {
                    return Forbid();
                }

                var summary = await _transactionService.GetAccountTransactionSummaryAsync(
                    accountId, startDate, endDate);

                var transactions = await _transactionService.GetTransactionsByDateRangeAsync(
                    accountId,
                    startDate ?? DateTime.UtcNow.AddDays(-30),
                    endDate ?? DateTime.UtcNow);

                return Ok(new
                {
                    accountId = accountId,
                    startDate = startDate,
                    endDate = endDate,
                    totalAmount = summary,
                    transactionCount = transactions.Count(),
                    currentBalance = account.Balance,
                    availableBalance = account.AvailableBalance
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transaction summary for account {AccountId}", accountId);
                return StatusCode(500, new { message = "An error occurred while retrieving transaction summary" });
            }
        }

        /// <summary>
        /// 撤销交易（仅管理员）
        /// </summary>
        /// <param name="id">交易ID</param>
        /// <param name="request">撤销请求</param>
        /// <returns>撤销结果</returns>
        [HttpPost("{id}/reverse")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReverseTransaction(int id, [FromBody] ReverseTransactionRequest request)
        {
            try
            {
                _logger.LogInformation("Reversing transaction {TransactionId}", id);

                var transaction = await _transactionService.GetTransactionByIdAsync(id);
                if (transaction == null)
                {
                    return NotFound(new { message = "Transaction not found" });
                }

                var result = await _transactionService.ReverseTransactionAsync(id, request.Reason);

                if (!result)
                {
                    return BadRequest(new { message = "Failed to reverse transaction" });
                }

                _logger.LogInformation("Transaction {TransactionId} reversed successfully", id);

                return Ok(new { message = "Transaction reversed successfully", transactionId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reversing transaction {TransactionId}", id);
                return StatusCode(500, new { message = "An error occurred while reversing the transaction" });
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

        private static TransactionResponse MapToTransactionResponse(Transaction transaction)
        {
            return new TransactionResponse
            {
                Id = transaction.Id,
                TransactionId = transaction.TransactionId,
                FromAccountId = transaction.FromAccountId,
                ToAccountId = transaction.ToAccountId,
                Amount = transaction.Amount,
                TransactionType = transaction.TransactionType,
                Description = transaction.Description,
                Status = transaction.Status,
                CreatedAt = transaction.CreatedAt
            };
        }

        private static TransactionDetailResponse MapToTransactionDetailResponse(Transaction transaction)
        {
            return new TransactionDetailResponse
            {
                Id = transaction.Id,
                TransactionId = transaction.TransactionId,
                FromAccountId = transaction.FromAccountId,
                ToAccountId = transaction.ToAccountId,
                Amount = transaction.Amount,
                TransactionType = transaction.TransactionType,
                Description = transaction.Description,
                Status = transaction.Status,
                CreatedAt = transaction.CreatedAt,
                ProcessedAt = transaction.ProcessedAt
            };
        }
    }

    // DTO类定义
    public class DepositRequest
    {
        public int AccountId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }

    public class WithdrawRequest
    {
        public int AccountId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }

    public class TransferRequest
    {
        public int FromAccountId { get; set; }
        public int ToAccountId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }

    public class ReverseTransactionRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class TransactionResponse
    {
        public int Id { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public int? FromAccountId { get; set; }
        public int? ToAccountId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class TransactionDetailResponse : TransactionResponse
    {
        public DateTime ProcessedAt { get; set; }
    }
}