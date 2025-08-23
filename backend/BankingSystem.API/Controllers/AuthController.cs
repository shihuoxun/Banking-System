using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BankingSystem.Core.Entities;
using BankingSystem.Core.Services;
using BankingSystem.Security.Services;
using System.ComponentModel.DataAnnotations;

namespace BankingSystem.API.Controllers
{
    [ApiController]
    [Route("api/auth")]  // 明确指定小写路由
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IUserService userService,
            IJwtTokenService jwtTokenService,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ILogger<AuthController> logger)
        {
            _userService = userService;
            _jwtTokenService = jwtTokenService;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        /// <summary>
        /// 用户注册
        /// </summary>
        /// <param name="request">注册请求</param>
        /// <returns>注册结果</returns>
        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Registration attempt for email: {Email}", request.Email);

                // 检查用户是否已存在
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return BadRequest(new ErrorResponse
                    {
                        Message = "用户已存在",
                        Details = "该邮箱已被注册"
                    });
                }

                // 创建新用户
                var user = new User
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PhoneNumber = request.PhoneNumber,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    return BadRequest(new ErrorResponse
                    {
                        Message = "注册失败",
                        Details = string.Join(", ", result.Errors.Select(e => e.Description))
                    });
                }

                // 分配默认角色
                await _userManager.AddToRoleAsync(user, "Customer");

                // 获取用户角色
                var roles = await _userManager.GetRolesAsync(user);

                // 生成 JWT Token
                var token = await _jwtTokenService.GenerateTokenAsync(user.Id, user.Email!, roles);

                _logger.LogInformation("User registered successfully: {UserId}", user.Id);

                return Ok(new AuthResponse
                {
                    Token = token,
                    RefreshToken = await _jwtTokenService.GenerateRefreshTokenAsync(),
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Email = user.Email ?? string.Empty,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        PhoneNumber = user.PhoneNumber
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "注册过程中发生错误",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="request">登录请求</param>
        /// <returns>登录结果</returns>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation("Login attempt for email: {Email}", request.Email);

                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return Unauthorized(new ErrorResponse
                    {
                        Message = "登录失败",
                        Details = "邮箱或密码错误"
                    });
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);
                if (!result.Succeeded)
                {
                    if (result.IsLockedOut)
                    {
                        return Unauthorized(new ErrorResponse
                        {
                            Message = "账户已锁定",
                            Details = "由于多次登录失败，账户已被临时锁定"
                        });
                    }

                    return Unauthorized(new ErrorResponse
                    {
                        Message = "登录失败",
                        Details = "邮箱或密码错误"
                    });
                }

                // 更新最后登录时间
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                // 获取用户角色
                var roles = await _userManager.GetRolesAsync(user);

                // 生成 JWT Token
                var token = await _jwtTokenService.GenerateTokenAsync(user.Id, user.Email!, roles);

                _logger.LogInformation("User logged in successfully: {UserId}", user.Id);

                return Ok(new AuthResponse
                {
                    Token = token,
                    RefreshToken = await _jwtTokenService.GenerateRefreshTokenAsync(),
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Email = user.Email ?? string.Empty,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        PhoneNumber = user.PhoneNumber
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "登录过程中发生错误",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// 刷新访问令牌
        /// </summary>
        /// <param name="request">刷新令牌请求</param>
        /// <returns>新的访问令牌</returns>
        [HttpPost("refresh-token")]
        [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var principal = _jwtTokenService.GetPrincipalFromExpiredToken(request.Token);
                if (principal == null)
                {
                    return Unauthorized(new ErrorResponse
                    {
                        Message = "无效的令牌",
                        Details = "令牌格式错误或已过期"
                    });
                }

                var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ErrorResponse
                    {
                        Message = "无效的令牌",
                        Details = "令牌中缺少用户信息"
                    });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Unauthorized(new ErrorResponse
                    {
                        Message = "用户不存在",
                        Details = "令牌中的用户已不存在"
                    });
                }

                // TODO: 验证 refresh token 的有效性
                // 这里应该检查数据库中存储的 refresh token

                // 获取用户角色
                var roles = await _userManager.GetRolesAsync(user);

                var newToken = await _jwtTokenService.GenerateTokenAsync(user.Id, user.Email!, roles);
                var newRefreshToken = await _jwtTokenService.GenerateRefreshTokenAsync();

                return Ok(new TokenResponse
                {
                    Token = newToken,
                    RefreshToken = newRefreshToken
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "刷新令牌过程中发生错误",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取当前用户信息
        /// </summary>
        /// <returns>用户信息</returns>
        [HttpGet("profile")]
        [Authorize]
        [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ErrorResponse
                    {
                        Message = "无效的令牌",
                        Details = "令牌中缺少用户信息"
                    });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new ErrorResponse
                    {
                        Message = "用户不存在",
                        Details = "用户信息未找到"
                    });
                }

                return Ok(new UserInfo
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user profile");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "获取用户信息时发生错误",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// 用户登出
        /// </summary>
        /// <returns>登出结果</returns>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation("User logged out: {UserId}", userId);

                // TODO: 将令牌加入黑名单或删除 refresh token
                // 这里可以实现令牌失效机制

                await _signInManager.SignOutAsync();

                return Ok(new { Message = "登出成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "登出过程中发生错误",
                    Details = ex.Message
                });
            }
        }
    }

    // DTO 类定义
    public class RegisterRequest
    {
        [Required(ErrorMessage = "邮箱是必填项")]
        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "密码是必填项")]
        [MinLength(8, ErrorMessage = "密码至少需要8位")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "名字是必填项")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "姓氏是必填项")]
        public string LastName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "电话号码格式不正确")]
        public string? PhoneNumber { get; set; }
    }

    public class LoginRequest
    {
        [Required(ErrorMessage = "邮箱是必填项")]
        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "密码是必填项")]
        public string Password { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public UserInfo User { get; set; } = new();
    }

    public class TokenResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class UserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
    }

    public class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}