// What this file is for: small helpers that turn values into the exact shapes the tree uses (UTC milliseconds, yyyy-MM-dd dates, key text)
// and read them back safely. Empty values are never stored.
// How it fits in: Repository folder. Every repository uses these so the tree always looks the same.
using System.Globalization;   // fixed date formats, whatever the computer's language is
using System.Text.Json.Nodes;
using SensorGrid.Domain;      // the enums from step 2

namespace SensorGrid.Repository;

/// <summary>Converts between C# values and the JSON stored in the Realtime Database.</summary>
public static class TreeJson
{
    /// <summary>UTC time to milliseconds since 1970 (how the tree stores times).</summary>
    public static long ToMs(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    /// <summary>Milliseconds since 1970 back to a UTC time.</summary>
    public static DateTime FromMs(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

    /// <summary>A date as yyyy-MM-dd.</summary>
    public static string DateText(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>yyyy-MM-dd back to a date (an unreadable value gives the default date instead of an error).</summary>
    public static DateTime ParseDate(string? s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : default;

    /// <summary>The key of a reading or status: the capture time as yyyyMMddHHmm in UTC.</summary>
    public static string TimeKey(DateTime utc) => utc.ToUniversalTime().ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);

    /// <summary>A yyyyMMddHHmm key back to a UTC time.</summary>
    public static DateTime ParseTimeKey(string key) =>
        DateTime.TryParseExact(key, "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : default;

    /// <summary>Adds a value to an object unless it is empty (null or blank text).</summary>
    public static void Put(JsonObject target, string name, object? value)
    {
        switch (value)
        {
            case null: return;                                             // nothing to store
            case string s when string.IsNullOrWhiteSpace(s): return;      // empty text is not stored either
            case string s: target[name] = s; break;
            case bool b: target[name] = b; break;
            case int i: target[name] = i; break;
            case long l: target[name] = l; break;
            case double d: target[name] = d; break;
            case DateTime t: target[name] = ToMs(t); break;                // times are stored as milliseconds
            default: target[name] = value.ToString(); break;               // anything else as text
        }
    }

    /// <summary>Reads a text field, or the fallback when it is missing.</summary>
    public static string Str(JsonNode? node, string name, string fallback = "") =>
        node is JsonObject o && o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;

    /// <summary>Reads a true/false field, or the fallback when it is missing.</summary>
    public static bool Bool(JsonNode? node, string name, bool fallback = false) =>
        node is JsonObject o && o[name] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    /// <summary>Reads a number of any stored type (int, long, double...), so every number the database sends is accepted.</summary>
    private static double? Number(JsonNode? node, string name)
    {
        if (node is not JsonObject o || o[name] is not JsonValue v) return null; // missing field
        if (v.TryGetValue<double>(out var d)) return d;
        if (v.TryGetValue<long>(out var l)) return l;
        if (v.TryGetValue<int>(out var i)) return i;
        if (v.TryGetValue<decimal>(out var m)) return (double)m;
        if (v.TryGetValue<float>(out var f)) return f;
        return null;                                                       // not a number
    }

    /// <summary>Reads a whole number (used for millisecond times).</summary>
    public static long? Long(JsonNode? node, string name)
    {
        if (node is JsonObject o && o[name] is JsonValue v && v.TryGetValue<long>(out var exact)) return exact; // keeps full precision for millisecond times
        return Number(node, name) is { } n ? (long)n : null;
    }

    /// <summary>Reads a number with decimals (used for readings and thresholds).</summary>
    public static double? Num(JsonNode? node, string name) => Number(node, name);

    /// <summary>Reads a millisecond time field as a UTC time.</summary>
    public static DateTime? Time(JsonNode? node, string name) => Long(node, name) is { } ms ? FromMs(ms) : null;

    // ------------------------------------------------------------------ names used in the tree

    /// <summary>Parameter names exactly as the tree spells them.</summary>
    public static string Key(MeasuredParameter p) => p switch
    {
        MeasuredParameter.Moisture => "soil_moisture",
        MeasuredParameter.Temperature => "soil_temperature",
        MeasuredParameter.Conductivity => "conductivity",
        MeasuredParameter.Ph => "ph",
        MeasuredParameter.Nitrogen => "nitrogen",
        MeasuredParameter.Phosphorus => "phosphorus",
        MeasuredParameter.Potassium => "potassium",
        MeasuredParameter.Battery => "battery",
        _ => throw new ArgumentOutOfRangeException(nameof(p))
    };

    /// <summary>The tree's parameter name back to the enum (null when unknown).</summary>
    public static MeasuredParameter? ParseParameter(string? key) => key switch
    {
        "soil_moisture" => MeasuredParameter.Moisture,
        "soil_temperature" => MeasuredParameter.Temperature,
        "conductivity" => MeasuredParameter.Conductivity,
        "ph" => MeasuredParameter.Ph,
        "nitrogen" => MeasuredParameter.Nitrogen,
        "phosphorus" => MeasuredParameter.Phosphorus,
        "potassium" => MeasuredParameter.Potassium,
        "battery" => MeasuredParameter.Battery,
        _ => null
    };

    // roles are stored as "Administrator" or "Standard User" (with a space)
    public static string RoleText(UserRole r) => r == UserRole.Administrator ? "Administrator" : "Standard User";

    public static UserRole ParseRole(string? s) => s == "Administrator" ? UserRole.Administrator : UserRole.StandardUser;

    // report formats are stored in capitals: "PDF" or "CSV"
    public static string FormatText(ReportFormat f) => f == ReportFormat.Pdf ? "PDF" : "CSV";

    public static ReportFormat ParseFormat(string? s) => s == "CSV" ? ReportFormat.Csv : ReportFormat.Pdf;

    // report types as the tree spells them
    public static string KindText(ReportKind k) => k switch
    {
        ReportKind.NutrientSummary => "Nutrient summary",
        ReportKind.RawReadings => "Raw readings",
        _ => "Breach log"
    };

    public static ReportKind ParseKind(string? s) => s switch
    {
        "Nutrient summary" => ReportKind.NutrientSummary,
        "Raw readings" => ReportKind.RawReadings,
        _ => ReportKind.BreachLog
    };

    /// <summary>Reads any enum from its stored text (ignoring capitals), or the fallback when it does not match.</summary>
    public static T ParseEnum<T>(string? s, T fallback) where T : struct, Enum => Enum.TryParse<T>(s, true, out var v) ? v : fallback;
}