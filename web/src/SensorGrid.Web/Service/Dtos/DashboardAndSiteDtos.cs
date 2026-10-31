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
/// <summary>One point of a trend line: T is the time (milliseconds since 1970, UTC), V the value.</summary>
public sealed record TrendPointDto(long T, double V);
public sealed record TrendSeriesDto(string Name, string Colour, IReadOnlyList<TrendPointDto> Points);
public sealed record TrendLabelDto(long T, string Text);
/// <summary>The Dashboard trend chart: one line per province for one value, from From to To. Live means the page reloads it every 10 seconds.</summary>
public sealed record TrendChartDto(string Caption, string Unit, int Decimals, long From, long To, double? BandMin, double? BandMax,
    IReadOnlyList<TrendLabelDto> Labels, IReadOnlyList<TrendSeriesDto> Series, bool Live);
public sealed record SiteCardDto(string SiteId, string Name, string Coordinates, string StatusLabel, Tone Tone, string CropLine, int Sensors, int ReadingsToday, int InRangePercent);
public sealed record AverageRowDto(string Name, IReadOnlyList<string> Values, string StatusText, Tone Tone);
public sealed record FeedItemDto(string When, string Province, string Text, Tone Tone);
public sealed record SitesPageDto(string Subtitle, string AverageCaption, IReadOnlyList<SiteCardDto> Cards, IReadOnlyList<AverageRowDto> Averages, IReadOnlyList<FeedItemDto> Feed);
/// <summary>One live reading on the Site detail page. Position (0 to 100) places the value on the gauge, where the target band covers 18 to 76.</summary>
public sealed record ParamCardDto(string Label, string Value, string Unit, string StatusLabel, Tone Tone, string Colour, int Position, string Target);
public sealed record SiteDetailDto(string SiteId, string Name, string Subtitle, IReadOnlyList<ParamCardDto> Parameters, string Summary, Tone SummaryTone,
    string Updated, IReadOnlyList<SensorRowDto> Sensors, string SensorsCaption);

/// <summary>Maps a sensor to the row shown in the sensor tables.</summary>
public sealed record SensorRowDto(string SensorId, string Province, string Installed, string LastReading, string Firmware, string StateLabel, Tone Tone)
{
    /// <summary>The live part (last reading, online or offline) comes from the gateway's latest row; a sensor without one has not reported yet.</summary>
    public static SensorRowDto From(Sensor s, string province, SensorStatus? status, DateTime nowUtc)
    {
        var state = SensorStateRule.Evaluate(s, status, nowUtc);
        var tone = state == SensorState.Online ? Tone.Ok : Tone.Off;
        return new SensorRowDto(s.SensorId, province, s.InstalledDate.ToString("dd MMM yyyy", Display.Culture), Display.LastReading(status?.LastCommunication, nowUtc),
            s.Firmware, Display.StateLabel(state), tone);
    }
}
