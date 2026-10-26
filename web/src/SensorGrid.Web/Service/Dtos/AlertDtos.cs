// What this file is for: alert rows, the alerts page, the note dialog and the bell panel items.
// How it fits in: Service folder, Dtos folder. A DTO is a small class that carries ready-to-show data from a service to a page.

using SensorGrid.Domain;

namespace SensorGrid.Service;

/// <summary>Which colour group a table row belongs to.</summary>
public enum AlertKindView { Critical, Warning, Offline }
/// <summary>One row of the alerts table, already formatted for display.</summary>
public sealed record AlertRowDto(string AlertId, string Raised, AlertKindView Kind, string KindLabel, string Province, string Sensor,
    string Parameter, string Reading, string Threshold, string Owner, AlertStatus Status);
/// <summary>Everything the Alerts page shows.</summary>
public sealed record AlertsPageDto(string Subtitle, int Open, int Acknowledged, int ResolvedThisMonth, string AverageClose,
    IReadOnlyList<AlertRowDto> Rows, IReadOnlyList<(string SiteId, string Name)> Provinces);
/// <summary>The note stored on an alert, for the "View note" dialog.</summary>
public sealed record AlertNoteDto(string AlertId, string Sensor, string Status, string Note, string Owner, string When);
/// <summary>One line in the bell panel.</summary>
public sealed record BellItemDto(string NotificationId, string AlertId, string Title, string Detail, string When, bool IsRead, AlertKindView Kind);
