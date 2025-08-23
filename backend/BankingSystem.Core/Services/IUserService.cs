using BankingSystem.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace BankingSystem.Core.Services
{
    public interface IUserService
    {
        Task<IdentityResult> RegisterAsync(string email, string password, string firstName, string lastName);
        Task<(bool Success, string Token, string RefreshToken, User User)> LoginAsync(string email, string password);
        Task<(bool Success, string Token)> RefreshTokenAsync(string refreshToken);
        Task<bool> LogoutAsync(string userId);
        Task<User?> GetUserByIdAsync(string userId);
        Task<User?> GetUserByEmailAsync(string email);
        Task<IdentityResult> UpdateUserAsync(User user);
        Task<IdentityResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
        Task<bool> IsEmailExistsAsync(string email);
    }
}
