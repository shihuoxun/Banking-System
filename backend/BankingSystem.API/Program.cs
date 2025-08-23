using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using BankingSystem.Data;
using BankingSystem.Core.Entities;
using BankingSystem.Core.Services;           // 用户服务接口
using BankingSystem.Security.Services;      // JWT服务
using FluentValidation.AspNetCore;
using AutoMapper;

// 为数据层服务创建别名以避免命名冲突
using DataServices = BankingSystem.Data.Services;

var builder = WebApplication.CreateBuilder(args);

// 配置 Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/banking-system-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();

// 更新的 FluentValidation 配置
builder.Services.AddFluentValidationAutoValidation()
    .AddFluentValidationClientsideAdapters();

// 配置数据库上下文 - 支持多数据库
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var environment = builder.Environment.EnvironmentName;

builder.Services.AddDbContext<BankingDbContext>(options =>
{
    if (environment == "Production" && connectionString?.Contains("Host=") == true)
    {
        // Use PostgreSQL for production
        Log.Information("Configuring PostgreSQL database for production environment");
        options.UseNpgsql(connectionString, 
            b => b.MigrationsAssembly("BankingSystem.API"));
    }
    else
    {
        // Use SQLite for development
        Log.Information("Configuring SQLite database for development environment");
        options.UseSqlite(connectionString ?? "Data Source=BankingSystemDb_Dev.db",
            b => b.MigrationsAssembly("BankingSystem.API"));
    }
});

// 配置 Identity
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    // 密码策略
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 1;

    // 账户锁定策略
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    options.Lockout.MaxFailedAccessAttempts = 3;
    options.Lockout.AllowedForNewUsers = true;

    // 用户策略
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false; // 开发时设为false，生产时应为true
})
.AddEntityFrameworkStores<BankingDbContext>()
.AddDefaultTokenProviders();

// 配置 JWT 认证 - 支持环境变量和配置文件
var jwtSecret = builder.Configuration["JWT__SecretKey"] ?? 
                builder.Configuration["JwtSettings:Secret"] ?? 
                throw new ArgumentNullException("JWT Secret not found in configuration");

var jwtIssuer = builder.Configuration["JWT__Issuer"] ?? 
                builder.Configuration["JwtSettings:Issuer"] ?? 
                "BankingSystemAPI";

var jwtAudience = builder.Configuration["JWT__Audience"] ?? 
                  builder.Configuration["JwtSettings:Audience"] ?? 
                  "BankingSystemUsers";

var key = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = environment == "Production"; // 生产环境要求HTTPS
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// 配置授权策略
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireCustomerRole", policy => policy.RequireRole("Customer"));
});

// 配置 AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// 配置 CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:4200") // Angular 开发服务器
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
    
    options.AddPolicy("ProductionPolicy", policy =>
    {
        policy.WithOrigins("https://yourdomain.com") // 生产域名
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 配置 Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Banking System API",
        Version = "v1",
        Description = "A secure banking system API"
    });

    // 配置 JWT 在 Swagger 中的认证
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// 添加健康检查
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BankingDbContext>();

// 注册业务服务 - 修复后的版本
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAccountService, DataServices.AccountService>();
builder.Services.AddScoped<ITransactionService, DataServices.TransactionService>();

// 注册安全服务
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Banking System API V1");
    });
    app.UseCors("DevelopmentPolicy");
}
else
{
    // 生产环境仍可启用Swagger用于API文档
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Banking System API V1");
    });
    app.UseCors("ProductionPolicy");
}

// 健康检查端点
app.MapHealthChecks("/health");

// 安全头配置
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

// HTTPS重定向（生产环境）
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// 认证和授权中间件顺序很重要
app.UseAuthentication();
app.UseAuthorization();

// 请求日志记录
app.UseSerilogRequestLogging();

app.MapControllers();

// 数据库和角色初始化
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    try
    {
        // 应用迁移（生产环境）或确保数据库创建（开发环境）
        if (app.Environment.IsProduction())
        {
            Log.Information("Applying database migrations for production environment");
            await context.Database.EnsureCreatedAsync();
        }
        else
        {
            Log.Information("Ensuring database is created for development environment");
            await context.Database.EnsureCreatedAsync();
        }
        
        Log.Information("Database connection verified successfully");

        // 创建默认角色
        var roles = new[] { "Admin", "Customer" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                Log.Information("Created role: {Role}", role);
            }
            else
            {
                Log.Information("Role already exists: {Role}", role);
            }
        }

        Log.Information("Role initialization completed successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Database or role initialization failed during startup");
        throw;
    }
}

Log.Information("Banking System API starting up on {Environment} environment...", app.Environment.EnvironmentName);

app.Run();