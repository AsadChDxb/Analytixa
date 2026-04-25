using System.Text;
using AdHocReporting.Application.Interfaces;
using AdHocReporting.Infrastructure.Middleware;
using AdHocReporting.Infrastructure.Persistence;
using AdHocReporting.Infrastructure.Security;
using AdHocReporting.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AdHocReporting.Infrastructure.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDataProtection();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddDbContext<AdHocDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDatasourceService, DatasourceService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddSingleton<SettingsSecretProtectionService>();
        services.AddHttpClient("OpenAI", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3);
        });

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("ManageDatasource", p => p.RequireClaim("permission", "ManageDatasource"));
            options.AddPolicy("ManageUsers", p => p.RequireClaim("permission", "ManageUsers"));
            options.AddPolicy("ManageRoles", p => p.RequireClaim("permission", "ManageRoles"));
            options.AddPolicy("ManagePermissions", p => p.RequireClaim("permission", "ManagePermissions"));
            options.AddPolicy("ViewAuditLogs", p => p.RequireClaim("permission", "ViewAuditLogs"));
            options.AddPolicy("RunReport", p => p.RequireClaim("permission", "RunReport"));
            options.AddPolicy("ExportReport", p => p.RequireClaim("permission", "ExportReport"));
            options.AddPolicy("AdminLookup", p => p.RequireAssertion(ctx =>
                ctx.User.HasClaim("permission", "ManageUsers") ||
                ctx.User.HasClaim("permission", "ManageDatasource") ||
                ctx.User.HasClaim("permission", "ManageRoles") ||
                ctx.User.HasClaim("permission", "ViewAuditLogs")));
        });

        return services;
    }

    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
