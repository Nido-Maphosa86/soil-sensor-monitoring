// What this file is for: registers every service in the dependency injection container, and creates the first administrator of a new database.
// How it fits in: Service folder. Program.cs calls AddServiceLayer once, and EnsureFirstAdministratorAsync once at start-up (only when Firebase is connected).
// Services are singletons because the Firebase connection behind them is shared. There is no sample data: every record comes from Firebase.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SensorGrid.Domain;
using SensorGrid.Repository;

namespace SensorGrid.Service;

public static class ServiceLayer
{
    public static IServiceCollection AddServiceLayer(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<DataScope>();
        services.AddSingleton<Analytics>();
        services.AddSingleton<SensorLookup>();
        services.AddSingleton<SiteMetrics>();

        services.AddSingleton<ICsvWriter, CsvWriter>();
        services.AddSingleton<IPdfWriter, PdfWriter>();

        services.AddSingleton<IAlertEvaluator, AlertEvaluator>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IDashboardService, DashboardService>();
        services.AddSingleton<ISiteService, SiteService>();
        services.AddSingleton<ISensorService, SensorService>();
        services.AddSingleton<IReadingService, ReadingService>();
        services.AddSingleton<IThresholdService, ThresholdService>();
        services.AddSingleton<IAlertService, AlertService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IProfileService, ProfileService>();
        return services;
    }

    /// <summary>
    /// A new database has no accounts, so nobody could sign in to invite anyone. When "users" is empty and SensorGrid:FirstAdministrator:Email is set,
    /// this creates that administrator (status Invited) and Firebase emails them a "set your password" link. It does nothing once any account exists.
    /// A failure is logged, not thrown, so the web app still starts.
    /// </summary>
    public static async Task EnsureFirstAdministratorAsync(this IServiceProvider services)
    {
        var settings = services.GetRequiredService<SensorGridSettings>();
        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger("SensorGrid.FirstAdministrator");
        var email = settings.FirstAdministrator.Email.Trim();
        try
        {
            var users = services.GetRequiredService<IUserRepository>();
            if ((await users.GetAllAsync()).Count > 0) return; // accounts exist: invite people from the User accounts page instead
            if (email.Length == 0)
            {
                log.LogWarning("The database has no accounts yet. Set SensorGrid:FirstAdministrator:Email (and Name) and restart to create the first administrator.");
                return;
            }

            var gateway = services.GetRequiredService<IAuthGateway>();
            var name = settings.FirstAdministrator.Name.Trim() is { Length: > 0 } n ? n : email;
            var uid = await gateway.FindUserIdAsync(email) ?? await gateway.CreateAccountAsync(email, name);
            await users.SaveAsync(new AppUser
            {
                UserId = uid, Name = name, Email = email, Role = UserRole.Administrator, Status = AccountStatus.Invited,
                CreatedAt = services.GetRequiredService<IClock>().UtcNow,
                SiteIds = (await services.GetRequiredService<ILocationRepository>().GetAllAsync()).Select(s => s.SiteId).ToList()
            });
            await gateway.SendInvitationAsync(email);
            log.LogInformation("First administrator created: {Email}. Firebase has emailed them a link to set their password.", email);
        }
        catch (Exception e) when (e is RealtimeDatabaseException or AuthGatewayException or HttpRequestException)
        {
            log.LogError(e, "Could not create the first administrator ({Email}). Check the Firebase settings and restart.", email);
        }
    }
}
