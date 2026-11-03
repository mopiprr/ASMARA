
using ASMARA.API.Common.Security;
using Asmara.API.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ASMARA.API.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddAsmaraSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // JWT Options
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Key) && o.Key.Length >= 32,
                "Jwt:Key must be at least 32 characters (set it in User Secrets).")
            .ValidateOnStart();

        var jwt = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                "JWT configuration tidak ditemukan.");

        if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key belum dikonfigurasi atau panjangnya kurang dari 32 karakter.");
        }

        // JWT Signing Key
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwt.Key));

        // Authentication
        services.AddAuthentication(
                JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,

                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = key,

                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),

                        NameClaimType = "name",
                        RoleClaimType = "role"
                    };
            });

        // Authorization
        services.AddAuthorization();

        // Http Context
        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUser, CurrentUser>();

        // Password Hasher
        services.AddScoped<
            IPasswordHasher<Account>,
            PasswordHasher<Account>>();

        return services;
    }
}
