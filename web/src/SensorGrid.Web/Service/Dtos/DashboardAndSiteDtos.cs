// What this file is for: the dashboard, moisture chart, sites list and site detail pages, plus the colour tone of status words.
// How it fits in: Service folder, Dtos folder. A DTO is a small class that carries ready-to-show data from a service to a page.

using SensorGrid.Domain;

namespace SensorGrid.Service;

/// <summary>The colour family of a status word. The Web project turns these into the prototype colours.</summary>
public enum Tone { Ok, Warn, Bad, Info, Violet, Off }
public sealed record KpiDto(string Label, string Value, string Sub, Tone Tone);
public sealed record SiteStatusDto(string SiteId, string Name, string SensorsText, string StatusLabel, Tone Tone, int Percent);
public sealed record LatestReadingDto(string Time, string Province, string Sensor, IReadOnlyList<string> Values, string Status, Tone Tone);
public sealed record DashboardDto(string Subtitle, IReadOnlyList<KpiDto> Kpis, IReadOnlyList<SiteStatusDto> Sites, IReadOnlyList<LatestReadingDto> Latest, string ReadingsCaption);
public sealed record ChartSeriesDto(string SiteId, string Name, string Colour, IReadOnlyList<double> Values);
public sealed record MoistureChartDto(IReadOnlyList<string> Labels, IReadOnlyList<ChartSeriesDto> Series, string Caption);
public sealed record SiteCardDto(string SiteId, string Name, string Coordinates, string StatusLabel, Tone Tone, string CropLine, int Sensors, int ReadingsToday, int InRangePercent);
public sealed record AverageRowDto(string Name, IReadOnlyList<string> Values, string StatusText, Tone Tone);
public sealed record FeedItemDto(string When, string Province, string Text, Tone Tone);
public sealed record SitesPageDto(string Subtitle, string AverageCaption, IReadOnlyList<SiteCardDto> Cards, IReadOnlyList<AverageRowDto> Averages, IReadOnlyList<FeedItemDto> Feed);
public sealed record ParamCardDto(string Label, string Value, string Unit, string StatusLabel, Tone Tone, string Colour, int Position);
public sealed record SiteDetailDto(string SiteId, string Name, string Subtitle, IReadOnlyList<ParamCardDto> Parameters, string Summary, Tone SummaryTone,
    string Updated, IReadOnlyList<SensorRowDto> Sensors, string SensorsCaption);

/// <summary>Maps a sensor to the row shown in the sensor tables.</summary>
public sealed record SensorRowDto(string SensorId, string Province, string Installed, string LastReading, string Battery, string Signal, string Firmware, string StateLabel, Tone Tone)
{
    /// <summary>The live part (battery, signal, last reading) comes from the newest sensor_status; a sensor without one has not reported yet.</summary>
    public static SensorRowDto From(Sensor s, string province, SensorStatus? status, DateTime nowUtc)
    {
        var state = SensorStateRule.Evaluate(s, status, nowUtc);
        var tone = state switch { SensorState.Online => Tone.Ok, SensorState.Offline => Tone.Off, _ => Tone.Warn };
        return new SensorRowDto(s.SensorId, province, s.InstalledDate.ToString("dd MMM yyyy", Display.Culture), Display.LastReading(status?.LastCommunication, nowUtc),
            status is null ? "–" : $"{status.BatteryPercent}%", status is null ? "–" : Display.SignalLabel(status.Signal), s.Firmware, Display.StateLabel(state), tone);
    }
}
