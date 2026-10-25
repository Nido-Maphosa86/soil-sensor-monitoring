// What this file is for: the plain data objects of SensorGrid. Each one matches a branch of the Realtime Database tree described in the database design (SRS v4.0).
// How it fits in: Domain folder. They hold data and a few small helpers; they know nothing about screens or storage.
// All times are UTC. A "Site" is a location (province) and its id is the location id, for example "FS".
namespace SensorGrid.Domain;

/// <summary>users/{uid} plus user_locations/{uid}. The password is never here: Firebase Authentication holds it.</summary>
public class AppUser
{
    public string UserId { get; set; } = "";                         // the Firebase Authentication user id (uid)
    public string Name { get; set; } = "";                           // full name shown in the top bar
    public string Email { get; set; } = "";                          // the sign-in email
    public UserRole Role { get; set; }                               // Administrator or Standard User
    public AccountStatus Status { get; set; } = AccountStatus.Active;// Active, Invited or Deactivated
    public DateTime CreatedAt { get; set; }                          // when the account was created
    /// <summary>Extra field used for the "Last active" column. Not in the shared tree contract; readers that do not know it ignore it.</summary>
    public DateTime? LastActiveAt { get; set; }                      // null until the first sign in
    public bool NotifyBreach { get; set; } = true;                   // wants a notification when a reading breaches a band
    public bool NotifyOffline { get; set; } = true;                  // wants a notification when a sensor goes offline
    public bool NotifyDaily { get; set; }                            // wants the daily summary
    /// <summary>The location ids assigned to this person (user_locations). Administrators always see all.</summary>
    public List<string> SiteIds { get; set; } = new();               // for example ["FS", "GP"]
}

/// <summary>locations/{location_id}. One site per province.</summary>
public class Site
{
    public string SiteId { get; set; } = "";             // the location id, for example "FS"
    public string Name { get; set; } = "";               // for example "Free State"
    public string Description { get; set; } = "";
    public string Crop { get; set; } = "Pigeon pea";     // the crop grown at this site
    public double Latitude { get; set; }                 // GPS position of the site
    public double Longitude { get; set; }
    public DateTime PlantingDate { get; set; }           // a date only (no time zone)
    public DateTime CreatedAt { get; set; }
    /// <summary>Sensor codes start with the location id, for example "FS-06".</summary>
    public string Prefix => SiteId;                      // read-only: always the same as the site id
}

/// <summary>sensors/{loc}/{sensor_id}. The settings of a probe. Its live health is a SensorStatus.</summary>
public class Sensor
{
    public string SensorId { get; set; } = "";           // the sensor code, for example "FS-03"
    public string SiteId { get; set; } = "";             // the site it belongs to
    public string Name { get; set; } = "";
    public string Model { get; set; } = "SG-RS485-7";    // hardware model of the probe
    public bool IsActive { get; set; } = true;           // false once the sensor is retired
    /// <summary>When false, this sensor is left out of the site averages.</summary>
    public bool IncludeInAverage { get; set; } = true;
    public DateTime InstalledDate { get; set; }
    public int DepthCm { get; set; } = 15;               // depth below the surface, in centimetres
    public int ModbusSlaveId { get; set; }               // address on the RS485 bus; unique within a site
    public string Firmware { get; set; } = "v2.4.1";
}

/// <summary>sensor_status/{loc}/{sensor}/{key}. The newest one is the current health of the sensor.</summary>
public class SensorStatus
{
    public string SiteId { get; set; } = "";
    public string SensorId { get; set; } = "";
    public int BatteryPercent { get; set; }              // 0 to 100
    public string PowerStatus { get; set; } = "Battery"; // how the sensor is powered
    public SignalStrength Signal { get; set; }           // None, Weak, Fair or Strong
    /// <summary>"online" or "offline" as reported by the gateway. The app also checks the age of the last reading.</summary>
    public string Status { get; set; } = "online";
    public DateTime LastCommunication { get; set; }      // last time the sensor talked to the gateway
    public DateTime RecordedAt { get; set; }             // when this status was written
}

/// <summary>readings/{loc}/{sensor}/{key}. One set of seven values. The key is the capture time as yyyyMMddHHmm in UTC.</summary>
public class Reading
{
    public string SiteId { get; set; } = "";
    public string SensorId { get; set; } = "";
    public DateTime Timestamp { get; set; }              // when the values were captured
    public double Moisture { get; set; }                 // %
    public double Temperature { get; set; }              // °C
    public double Ph { get; set; }                       // pH
    public double Conductivity { get; set; }             // µS/cm
    public double Nitrogen { get; set; }                 // mg/kg
    public double Phosphorus { get; set; }               // mg/kg
    public double Potassium { get; set; }                // mg/kg

    /// <summary>Returns the value for one parameter (battery is not part of a reading).</summary>
    public double ValueOf(MeasuredParameter parameter) => parameter switch
    {
        MeasuredParameter.Moisture => Moisture,
        MeasuredParameter.Temperature => Temperature,
        MeasuredParameter.Ph => Ph,
        MeasuredParameter.Conductivity => Conductivity,
        MeasuredParameter.Nitrogen => Nitrogen,
        MeasuredParameter.Phosphorus => Phosphorus,
        MeasuredParameter.Potassium => Potassium,
        // asking for Battery (or anything else) is a programming mistake, so stop with a clear message
        _ => throw new ArgumentOutOfRangeException(nameof(parameter), "A reading has no value for this parameter.")
    };
}

/// <summary>thresholds/{loc}/{parameter}. The allowed band of one parameter in one location.</summary>
public class Threshold
{
    public string SiteId { get; set; } = "";
    public MeasuredParameter Parameter { get; set; }     // which value this band is for
    public double MinValue { get; set; }                 // below this is a breach
    public double MaxValue { get; set; }                 // above this is a breach
    /// <summary>What the band is based on, for example "Pigeon pea". Required when saving.</summary>
    public string Basis { get; set; } = "Pigeon pea";
    /// <summary>The user id of the administrator who last saved the band.</summary>
    public string UpdatedBy { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

/// <summary>alert_actions/{loc}/{alert_id}/{acknowledged|resolved}. What a person did to an alert and why.</summary>
public class AlertAction
{
    public string UserId { get; set; } = "";             // who acted
    public string Note { get; set; } = "";               // why (never empty)
    public DateTime ActedAt { get; set; }                // when
}

/// <summary>alerts/{loc}/{alert_id} plus its actions. Severity and Status are separate fields.</summary>
public class Alert
{
    public string AlertId { get; set; } = "";
    public string SiteId { get; set; } = "";
    public string SensorId { get; set; } = "";
    public AlertType AlertType { get; set; }             // Breach, Offline or Battery
    /// <summary>Null for offline alerts, because no parameter crossed a band.</summary>
    public MeasuredParameter? Parameter { get; set; }
    public AlertSeverity Severity { get; set; }          // Warning or Critical
    public AlertStatus Status { get; set; } = AlertStatus.Open; // Open, Acknowledged or Resolved
    public DateTime RaisedAt { get; set; }
    public double? ReadingValue { get; set; }            // the value that caused the alert
    /// <summary>The limit that was crossed (a minimum or a maximum).</summary>
    public double? ThresholdValue { get; set; }
    public AlertAction? Acknowledged { get; set; }       // null until someone acknowledges it
    public AlertAction? Resolved { get; set; }           // null until someone resolves it

    /// <summary>True when the limit crossed was the maximum (the reading is above the limit).</summary>
    public bool ThresholdIsMax => ReadingValue is { } r && ThresholdValue is { } t && r > t;
}

/// <summary>notifications/{uid}/{alert_id}. A line in a person's bell panel.</summary>
public class Notification
{
    public string UserId { get; set; } = "";             // who receives it
    public string AlertId { get; set; } = "";            // the alert it is about
    public string SiteId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }                     // false shows the unread dot
    public DateTime? ReadAt { get; set; }
}

/// <summary>reports/{report_id} plus report_locations. The file itself lives in Cloud Storage (or App_Data/reports on the web server).</summary>
public class Report
{
    public string ReportId { get; set; } = "";
    public string GeneratedBy { get; set; } = "";        // user id of the person who generated it
    public string Name { get; set; } = "";               // for example "Monthly nutrient summary — October 2026"
    public ReportKind Kind { get; set; }
    public ReportFormat Format { get; set; }             // PDF or CSV
    public DateTime PeriodStart { get; set; }            // the month the report covers
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string FilePath { get; set; } = "";           // where the file is stored
    public long SizeBytes { get; set; }
    public List<string> SiteIds { get; set; } = new();   // the provinces included in the report
}

/// <summary>data_loads/{load_id}. A record of one batch the gateway wrote. Only the gateway writes these.</summary>
public class DataLoad
{
    public string LoadId { get; set; } = "";
    public string Source { get; set; } = "";             // which gateway sent the batch
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public LoadOutcome Outcome { get; set; }             // Succeeded, Partial or Failed
    public int ReadingsWritten { get; set; }             // how many readings were saved
}