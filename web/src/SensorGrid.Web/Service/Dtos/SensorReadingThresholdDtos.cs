// What this file is for: the sensors list, register-sensor result, readings log and thresholds editor.
// How it fits in: Service folder, Dtos folder. A DTO is a small class that carries ready-to-show data from a service to a page.

using SensorGrid.Domain;

namespace SensorGrid.Service;

public sealed record SensorChipDto(string? SiteId, string Name, int Count);
public sealed record SensorsPageDto(string Subtitle, IReadOnlyList<SensorChipDto> Chips, IReadOnlyList<SensorRowDto> Rows, int Page, int PageCount, int Total, int FromIndex, int ToIndex);
/// <summary>Another sensor already uses the Modbus slave id the person typed.</summary>
public sealed record SlaveConflict(string SensorId, int SlaveId, int NextFree);
public sealed record RegisterResult(bool Success, IReadOnlyDictionary<string, string> Errors, SlaveConflict? Conflict);
/// <summary>The Readings log filters. Before / After are the key (push ID) of the row the page continues from (Older / Newer); none = the newest rows.</summary>
public sealed record ReadingFilter(DateTime? From, DateTime? To, string? SiteId, string? SensorId, string? Before = null, string? After = null);
public sealed record ReadingRowDto(string Timestamp, string Province, string Sensor, IReadOnlyList<string> Values);
/// <summary>One page of the Readings log. OlderKey / NewerKey are null when there is no page in that direction. Error explains a filter that cannot be used.</summary>
public sealed record ReadingsPageDto(ReadingFilter Filter, string Caption, IReadOnlyList<ReadingRowDto> Rows, string? OlderKey, string? NewerKey, bool IsNewest, string? Error,
    IReadOnlyList<(string SiteId, string Name)> Provinces, IReadOnlyList<string> SensorIds);
/// <summary>The CSV file of an export, or the reason it was not made (for example a period longer than 7 days).</summary>
public sealed record ReadingsExport(byte[]? Csv, string? Error);
public sealed record ThresholdCardDto(MeasuredParameter Parameter, string Label, string Unit, string Min, string Max, string Current, int Position, string Colour, string? Error);
public sealed record ThresholdEditorDto(string SiteId, IReadOnlyList<(string SiteId, string Name)> Provinces, string Crop, string Basis, string Caption, IReadOnlyList<ThresholdCardDto> Cards, string? BasisError = null);
public sealed record SaveThresholdsResult(bool Success, ThresholdEditorDto Editor);
