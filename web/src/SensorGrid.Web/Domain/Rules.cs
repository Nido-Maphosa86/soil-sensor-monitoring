// What this file is for: the business rules as small, testable code: breach, offline, low battery, threshold, alert life cycle, sensor code format.
// How it fits in: Domain folder. Services call these instead of repeating the logic. The same rules run in the Cloud Function later.
using System.Text.RegularExpressions; // for checking the sensor code format (FS-06)

namespace SensorGrid.Domain;

/// <summary>Gives the current time in UTC. Behind an interface so the rules never read the system clock directly.</summary>
public interface IClock
{
    DateTime UtcNow { get; } // "now", always in UTC
}

/// <summary>Thrown when someone tries to do something the business rules do not allow. The message is safe to show.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { } // the message is shown to the person as it is
}

/// <summary>Rule: a value outside its min/max band is a breach. The limits themselves are still inside the band.</summary>
public static class BreachRule
{
    // below the minimum or above the maximum is a breach; exactly on a limit is fine
    public static bool IsBreach(double value, double min, double max) => value < min || value > max;

    // the same check, taking the band from a stored threshold
    public static bool IsBreach(double value, Threshold band) => IsBreach(value, band.MinValue, band.MaxValue);

    /// <summary>Which limit was crossed: true for the maximum, false for the minimum.</summary>
    public static bool CrossedMax(double value, Threshold band) => value > band.MaxValue;
}

/// <summary>Rule: a sensor that has been silent for 2 hours or more is Offline. The same for every province.</summary>
public static class OfflineRule
{
    public static readonly TimeSpan SilenceLimit = TimeSpan.FromHours(2); // how long a sensor may be silent

    /// <summary>A sensor that never reported is treated as offline.</summary>
    public static bool IsOffline(DateTime? lastReadingUtc, DateTime nowUtc) =>
        lastReadingUtc is null                          // never reported at all
        || nowUtc - lastReadingUtc.Value >= SilenceLimit; // or silent for 2 hours or more
}

/// <summary>Rule: a battery below 25 percent raises a Warning.</summary>
public static class BatteryRule
{
    public const int AlertBelowPercent = 25; // the alert limit

    public static bool IsLow(int batteryPercent) => batteryPercent < AlertBelowPercent;
}

/// <summary>Rule: works out the state word shown for a sensor in tables.</summary>
public static class SensorStateRule
{
    /// <summary>Battery below this percentage is called "battery low" in tables (the alert itself needs less than 25).</summary>
    public const int LowBatteryLabelPercent = 55;

    public static SensorState Evaluate(Sensor sensor, SensorStatus? status, DateTime nowUtc)
    {
        // retired, never reported, or silent for 2 hours: Offline (checked first, because it matters most)
        if (!sensor.IsActive || status is null || OfflineRule.IsOffline(status.LastCommunication, nowUtc)) return SensorState.Offline;
        // still reporting, but the battery is getting low
        if (status.BatteryPercent < LowBatteryLabelPercent) return SensorState.BatteryLow;
        // still reporting, but the radio link is weak
        if (status.Signal == SignalStrength.Weak) return SensorState.NeedsAttention;
        // everything is fine
        return SensorState.Online;
    }
}

/// <summary>Rule: a threshold needs a minimum below its maximum, and a basis.</summary>
public static class ThresholdRule
{
    public static void EnsureValid(double min, double max, string? basis)
    {
        // min must be strictly below max (equal is not a band)
        if (!(min < max)) throw new DomainException("The minimum must be below the maximum.");
        // the basis says what the band is based on, so it cannot be empty
        if (string.IsNullOrWhiteSpace(basis)) throw new DomainException("A basis is required (for example the crop the band is based on).");
    }
}

/// <summary>Rule: a sensor code looks like FS-06: the location id, a dash and two digits. It is unique everywhere and never changes.</summary>
public static class SensorCodeRule
{
    // two capital letters, a dash, two digits; compiled once because it is used often
    private static readonly Regex Pattern = new(@"^[A-Z]{2}-\d{2}$", RegexOptions.Compiled);

    public static bool IsValidShape(string code) => Pattern.IsMatch(code);

    // FS-06 belongs to location FS
    public static bool BelongsTo(string code, string locationId) => code.StartsWith(locationId + "-", StringComparison.Ordinal);
}

/// <summary>Rule: a breach 15 percent or more beyond the limit it crossed is Critical; a smaller miss is a Warning. Low battery is a Warning.</summary>
public static class SeverityRule
{
    public const double CriticalFraction = 0.15; // 15%

    public static AlertSeverity For(double value, double limit)
    {
        if (limit == 0) return AlertSeverity.Critical;               // cannot work out a percentage of zero, so treat it as serious
        var miss = Math.Abs(value - limit) / Math.Abs(limit);        // how far past the limit, as a fraction of the limit
        return miss >= CriticalFraction ? AlertSeverity.Critical : AlertSeverity.Warning;
    }
}

/// <summary>Rule: the life cycle of an alert. Acknowledge and Resolve both need a note. The state only moves forward.</summary>
public static class AlertWorkflow
{
    /// <summary>True when the state may move from one value to the next (forward only).</summary>
    public static bool CanMove(AlertStatus from, AlertStatus to) =>
        (from == AlertStatus.Open && to == AlertStatus.Acknowledged) ||  // someone starts working on it
        (from == AlertStatus.Open && to == AlertStatus.Resolved) ||      // closed straight away
        (from == AlertStatus.Acknowledged && to == AlertStatus.Resolved); // closed after being worked on

    /// <summary>Open to Acknowledged. Records who and when. A note is required.</summary>
    public static AlertAction Acknowledge(Alert alert, string? note, string userId, DateTime nowUtc)
    {
        var clean = RequireNote(note); // refuses an empty note
        if (!CanMove(alert.Status, AlertStatus.Acknowledged)) throw new DomainException("Only an open alert can be acknowledged.");
        var action = new AlertAction { UserId = userId, Note = clean, ActedAt = nowUtc }; // who, why and when
        alert.Status = AlertStatus.Acknowledged; // move the state forward
        alert.Acknowledged = action;             // keep the action with the alert
        return action;                           // the caller saves the action and the new state together
    }

    /// <summary>Open or Acknowledged to Resolved. Records who and when. A note is required.</summary>
    public static AlertAction Resolve(Alert alert, string? note, string userId, DateTime nowUtc)
    {
        var clean = RequireNote(note); // refuses an empty note
        if (!CanMove(alert.Status, AlertStatus.Resolved)) throw new DomainException("This alert is already resolved.");
        var action = new AlertAction { UserId = userId, Note = clean, ActedAt = nowUtc };
        alert.Status = AlertStatus.Resolved;
        alert.Resolved = action;
        return action;
    }

    // a note made only of spaces counts as empty; otherwise it is stored without extra spaces at the ends
    private static string RequireNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) throw new DomainException("A note is required.");
        return note.Trim();
    }
}