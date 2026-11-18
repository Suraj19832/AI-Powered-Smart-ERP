using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartERP.Application.AI;
using SmartERP.Application.Common;
using SmartERP.Infrastructure.AI;
using SmartERP.Infrastructure.Persistence;
using SmartERP.Infrastructure.Security;

namespace SmartERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cs, sql => sql.CommandTimeout(60)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<IDateTime, SystemDateTime>();

        services.Configure<OpenAiOptions>(config.GetSection(OpenAiOptions.Section));
        services.AddHttpClient<IAiClient, OpenAiClient>((sp, http) =>
        {
            http.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<OpenAiOptions>>().Value.TimeoutSeconds);
        });

        services.AddSingleton(config.GetSection("App").Get<AppSettings>() ?? new AppSettings());
        services.AddScoped<DbSeeder>();
        return services;
    }
}
