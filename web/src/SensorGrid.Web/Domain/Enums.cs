// What this file is for: the fixed lists of choices (roles, alert types, states, parameters...) used by every layer.
// How it fits in: Domain folder. It references nothing, so every other part of the app can speak the same words.
namespace SensorGrid.Domain; // every model of the app lives in this namespace

/// <summary>The two kinds of account.</summary>
public enum UserRole
{
    Administrator, // manages everything: sites, sensors, thresholds and user accounts (web console only)
    StandardUser   // a field officer: reads only the provinces assigned to them (web console and mobile app)
}

/// <summary>Whether an account can sign in.</summary>
public enum AccountStatus
{
    Active,      // can sign in
    Invited,     // created by an administrator; becomes Active at the first sign in
    Deactivated  // can no longer sign in anywhere
}


/// <summary>Why an alert was raised.</summary>
public enum AlertType
{
    Breach,  // a reading left its min/max band
        Offline  // the sensor has been silent for 2 hours
}

/// <summary>How serious an alert is. Offline alerts are shown grey and do not use these colours.</summary>
public enum AlertSeverity
{
    Warning,  // amber: needs attention soon
    Critical  // red: needs attention now
}

/// <summary>Where an alert is in its life. It only moves forward: Open, then Acknowledged, then Resolved (or Open straight to Resolved).</summary>
public enum AlertStatus
{
    Open,         // nobody has acted on it yet
    Acknowledged, // someone is working on it (a note is required)
    Resolved      // closed (a note is required)
}

/// <summary>What a sensor measures: the seven values of a gateway row that the app uses.</summary>
public enum MeasuredParameter
{
    Moisture,     // soil moisture, %
    Temperature,  // soil temperature, °C
    Ph,           // pH level
    Conductivity, // electrical conductivity, µS/cm
    Nitrogen,     // N, mg/kg
    Phosphorus,   // P, mg/kg
    Potassium     // K, mg/kg
}

/// <summary>The live state of a sensor, shown in the sensor tables.</summary>
public enum SensorState
{
    Online,  // reported in the last minute
    Offline  // silent for 1 minute or more   // silent for 2 hours or more
}

/// <summary>The kinds of report the system can build.</summary>
public enum ReportKind
{
    NutrientSummary, // monthly averages of nitrogen, phosphorus and potassium per province
    RawReadings,     // every reading of the month (CSV only)
    BreachLog        // every alert raised in the month
}

/// <summary>The file formats a report can be downloaded in.</summary>
public enum ReportFormat
{
    Pdf,
    Csv
}

