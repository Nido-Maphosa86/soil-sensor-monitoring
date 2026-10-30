// What this file is for: registers the Firebase connection parts (database store, sign-in, report files, access token) and every repository.
// How it fits in: Repository folder. Program.cs calls AddRepositoryLayer once. Nothing is contacted here: the first call to Firebase happens on the
// first page request, so the app can start (and show the "waiting for the Firebase connection" page) before the settings are filled in.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SensorGrid.Domain;

namespace SensorGrid.Repository;

/// <summary>The real clock. Times are stored in UTC.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>Tells the web layer whether every Firebase setting is filled in, and which ones are missing.</summary>
public sealed class ConnectionState
{
    public ConnectionState(IReadOnlyList<string> missing) => Missing = missing;

    public IReadOnlyList<string> Missing { get; }

    public bool IsConnected => Missing.Count == 0;
}

public static class RepositoryLayer
{
    public static IServiceCollection AddRepositoryLayer(this IServiceCollection services, IConfiguration config)
    {
        var settings = config.GetSection("SensorGrid").Get<SensorGridSettings>() ?? new SensorGridSettings();
        services.AddSingleton(settings);
        services.AddSingleton(settings.Firebase);
        services.AddSingleton(new ConnectionState(settings.Firebase.Missing(settings.UseCloudStorageForReports)));

        // ------------------------------------------------------------------
        // CONNECT FIREBASE HERE: these four classes are the whole connection. They read SensorGrid:Firebase:* (see Settings.cs and README.md).
        // ------------------------------------------------------------------
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
        services.AddSingleton<IAccessTokenProvider, GoogleAccessTokenProvider>();
        services.AddSingleton<IRealtimeDatabaseStore, FirebaseRealtimeDatabaseStore>();
        services.AddSingleton<IAuthGateway, FirebaseAuthGateway>();
        // Cloud Storage needs the Blaze plan (open decision 22.7.4). Until then report files stay on the web server.
        if (settings.UseCloudStorageForReports) services.AddSingleton<IReportFileStore, CloudStorageReportFileStore>();
        else services.AddSingleton<IReportFileStore, LocalDiskReportFileStore>();
        services.AddSingleton<IClock, SystemClock>();

        // The repositories are written once, against IRealtimeDatabaseStore.
        services.AddSingleton<ILocationRepository, LocationRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<ISensorRepository, SensorRepository>();
        services.AddSingleton<ISensorStatusRepository, SensorStatusRepository>();
        services.AddSingleton<IReadingRepository, ReadingRepository>();
        services.AddSingleton<IThresholdRepository, ThresholdRepository>();
        services.AddSingleton<IAlertRepository, AlertRepository>();
        services.AddSingleton<INotificationRepository, NotificationRepository>();
        services.AddSingleton<IReportRepository, ReportRepository>();
        return services;
    }
}
