using BankingSystem.Core.Entities;
using BankingSystem.Core.Services;
using BankingSystem.Security.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Core.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ILogger<UserService> _logger;

        public UserService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IJwtTokenService jwtTokenService,
            ILogger<UserService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtTokenService = jwtTokenService;
            _logger = logger;
        }

        public async Task<IdentityResult> RegisterAsync(string email, string password, string firstName, string lastName)
        {
            try
            {
                _logger.LogInformation("Attempting to register user with email: {Email}", email);

                if (await IsEmailExistsAsync(email))
                {
                    _logger.LogWarning("Registration failed - email already exists: {Email}", email);
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code = "DuplicateEmail",
                        Description = "Email address is already registered."
                    });
                }

                var user = new User
                {
                    UserName = email,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Customer");
                    _logger.LogInformation("User registered successfully: {Email}", email);
                }
                else
                {
                    _logger.LogWarning("User registration failed for {Email}: {Errors}",
                        email, string.Join(", ", result.Errors.Select(e => e.Description)));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while registering user with email: {Email}", email);
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "RegistrationError",
                    Description = "An error occurred during registration."
                });
            }
        }

        public async Task<(bool Success, string Token, string RefreshToken, User User)> LoginAsync(string email, string password)
        {
            try
            {
                _logger.LogInformation("Login attempt for email: {Email}", email);

                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning("Login failed - user not found: {Email}", email);
                    return (false, string.Empty, string.Empty, null!);
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    var token = await _jwtTokenService.GenerateTokenAsync(user.Id, user.Email!, roles);
                    var refreshToken = await _jwtTokenService.GenerateRefreshTokenAsync();

                    user.LastLoginAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

                    _logger.LogInformation("User logged in successfully: {Email}", email);
                    return (true, token, refreshToken, user);
                }
                else if (result.IsLockedOut)
                {
                    _logger.LogWarning("Login failed - account locked out: {Email}", email);
                    return (false, string.Empty, string.Empty, null!);
                }
                else
                {
                    _logger.LogWarning("Login failed - invalid credentials: {Email}", email);
                    return (false, string.Empty, string.Empty, null!);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during login for email: {Email}", email);
                return (false, string.Empty, string.Empty, null!);
            }
        }

        public async Task<(bool Success, string Token)> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                _logger.LogInformation("Refresh token request");
                return (false, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during token refresh");
                return (false, string.Empty);
            }
        }

        public async Task<bool> LogoutAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Logout request for user: {UserId}", userId);
                await _signInManager.SignOutAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during logout for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            try
            {
                return await _userManager.FindByIdAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting user by ID: {UserId}", userId);
                return null;
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                return await _userManager.FindByEmailAsync(email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting user by email: {Email}", email);
                return null;
            }
        }

        public async Task<IdentityResult> UpdateUserAsync(User user)
        {
            try
            {
                _logger.LogInformation("Updating user: {UserId}", user.Id);
                user.UpdatedAt = DateTime.UtcNow;
                return await _userManager.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating user: {UserId}", user.Id);
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "UpdateError",
                    Description = "An error occurred while updating user."
                });
            }
        }

        public async Task<IdentityResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            try
            {
                _logger.LogInformation("Password change request for user: {UserId}", userId);

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code = "UserNotFound",
                        Description = "User not found."
                    });
                }

                return await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while changing password for user: {UserId}", userId);
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "PasswordChangeError",
                    Description = "An error occurred while changing password."
                });
            }
        }

        public async Task<bool> IsEmailExistsAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                return user != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking email existence: {Email}", email);
                return false;
            }
        }
    }
}
