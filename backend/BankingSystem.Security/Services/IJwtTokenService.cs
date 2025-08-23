// ===========================================
// IJwtTokenService.cs - 接口文件
// 位置: BankingSystem.Security/Services/IJwtTokenService.cs
// ===========================================

using System.Security.Claims;

namespace BankingSystem.Security.Services
{
    public interface IJwtTokenService
    {
        Task<string> GenerateTokenAsync(string userId, string email, IList<string> roles);
        Task<string> GenerateRefreshTokenAsync();
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
        Task<bool> ValidateTokenAsync(string token);
    }
}

// ===========================================
// JwtTokenService.cs - 实现文件
// 位置: BankingSystem.Security/Services/JwtTokenService.cs
// ===========================================

