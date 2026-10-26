// What this file is for: the data the top bar and sidebar need on every page (name, unread count, open alerts).
// How it fits in: Service folder, Dtos folder. A DTO is a small class that carries ready-to-show data from a service to a page.

using SensorGrid.Domain;

namespace SensorGrid.Service;

/// <summary>What the top bar, sidebar and bell need on every page.</summary>
public sealed record ShellDto(string UserId, string Name, string Initials, string Email, string RoleLabel, bool IsAdmin, int OpenAlerts, int Unread);
