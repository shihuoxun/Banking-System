using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BankingSystem.Security.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly string _secret;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _expiryMinutes;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
            _secret = _configuration["JwtSettings:Secret"] ?? throw new ArgumentNullException("JWT Secret not configured");
            _issuer = _configuration["JwtSettings:Issuer"] ?? throw new ArgumentNullException("JWT Issuer not configured");
            _audience = _configuration["JwtSettings:Audience"] ?? throw new ArgumentNullException("JWT Audience not configured");
            _expiryMinutes = int.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "60");
        }

        public async Task<string> GenerateTokenAsync(string userId, string email, IList<string> roles)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            };

            // 添加角色声明
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
                signingCredentials: credentials
            );

            return await Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
        }

        public async Task<string> GenerateRefreshTokenAsync()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return await Task.FromResult(Convert.ToBase64String(randomNumber));
        }

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
                ValidateLifetime = false // 这里设置为false，因为我们要检查过期的token
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Invalid token");
            }

            return principal;
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_secret);

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken validatedToken);
                return await Task.FromResult(true);
            }
            catch
            {
                return await Task.FromResult(false);
            }
        }
    }
}