// What this file is for: the Firebase connection settings and the few switches of the app.
// How it fits in: Repository folder. They are read from the "SensorGrid" section of appsettings.json, user-secrets or environment variables.
// CONNECT FIREBASE HERE: fill in the Firebase settings with user-secrets (see README.md). Until they are all filled in, the app starts in
// "waiting for the Firebase connection" mode and shows which settings are still missing. A key file or secret must never go into the repository.
namespace SensorGrid.Repository;

/// <summary>The whole "SensorGrid" section of the settings.</summary>
public sealed class SensorGridSettings
{
    public FirebaseSettings Firebase { get; set; } = new(); // the connection details (below)

    /// <summary>
    /// When true the web app itself runs the alert evaluator every minute (the fallback for the free Spark plan).
    /// When false the evaluator is expected to run elsewhere, for example as a scheduled Cloud Function (Blaze plan).
    /// </summary>
    public bool RunAlertEvaluatorInWebApp { get; set; }

    /// <summary>Save report files in Cloud Storage (needs the Blaze plan). False = save them in App_Data/reports on the web server.</summary>
    public bool UseCloudStorageForReports { get; set; }

    /// <summary>The public address of this web app, for example "https://sensorgrid.example.org". Invitation and reset emails link back to it.</summary>
    public string WebAppUrl { get; set; } = "";

    /// <summary>The first administrator. Used ONCE, when the database has no accounts yet. See README.md, "First administrator".</summary>
    public FirstAdministratorSettings FirstAdministrator { get; set; } = new();
}

/// <summary>Who becomes the first administrator of a brand-new database.</summary>
public sealed class FirstAdministratorSettings
{
    public string Email { get; set; } = ""; // Firebase emails this address a "set your password" link
    public string Name { get; set; } = "";
}

/// <summary>The details needed to reach the Firebase project.</summary>
public sealed class FirebaseSettings
{
    /// <summary>The Firebase project id, for example "sensorgrid-prod".</summary>
    public string ProjectId { get; set; } = "";

    /// <summary>The Realtime Database address, for example "https://sensorgrid-prod-default-rtdb.firebaseio.com".</summary>
    public string DatabaseUrl { get; set; } = "";

    /// <summary>The Web API key of the project (Project settings, General). Used for sign-in and password emails.</summary>
    public string WebApiKey { get; set; } = "";

    /// <summary>The Cloud Storage bucket for report files, for example "sensorgrid-prod.firebasestorage.app".</summary>
    public string StorageBucket { get; set; } = "";

    /// <summary>Full path to the service-account key (JSON). Keep it OUTSIDE the repository.</summary>
    public string ServiceAccountKeyPath { get; set; } = "";

    /// <summary>
    /// Local testing only: talk to the Firebase Emulator Suite instead of the real project (firebase emulators:start --only auth,database).
    /// DatabaseUrl then looks like "http://127.0.0.1:9000/?ns=sensorgrid-dev" and no service-account key is needed.
    /// </summary>
    public bool UseEmulators { get; set; }

    /// <summary>Where the Authentication emulator listens (only with UseEmulators).</summary>
    public string AuthEmulatorHost { get; set; } = "127.0.0.1:9099";

    /// <summary>The names of the settings that are still empty or wrong. An empty list means the app can connect.</summary>
    public IReadOnlyList<string> Missing(bool cloudStorageNeeded)
    {
        var missing = new List<string>();

        // small helper: remember the setting's full name when its value is empty
        void Need(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) missing.Add($"SensorGrid:Firebase:{name}");
        }

        Need(ProjectId, nameof(ProjectId));
        Need(DatabaseUrl, nameof(DatabaseUrl));
        Need(WebApiKey, nameof(WebApiKey));
        if (cloudStorageNeeded) Need(StorageBucket, nameof(StorageBucket)); // only needed when reports go to Cloud Storage
        if (!UseEmulators) // the emulators need no key file
        {
            Need(ServiceAccountKeyPath, nameof(ServiceAccountKeyPath));
            // the path is filled in, but the file is not there
            if (!string.IsNullOrWhiteSpace(ServiceAccountKeyPath) && !File.Exists(ServiceAccountKeyPath))
                missing.Add("SensorGrid:Firebase:ServiceAccountKeyPath (the file it points to does not exist)");
        }
        return missing;
    }

    /// <summary>The start of every Identity Toolkit (Firebase Authentication) address.</summary>
    public string AuthRoot => UseEmulators
        ? $"http://{AuthEmulatorHost.Trim().TrimEnd('/')}/identitytoolkit.googleapis.com/v1" // the local emulator
        : "https://identitytoolkit.googleapis.com/v1";                                     // the real Firebase
}