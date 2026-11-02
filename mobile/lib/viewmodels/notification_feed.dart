// SUMMARY: Builds the person's notification list: each notification (notifications/{uid}) joined to the alert it is about and the
// province name. Only alerts of the person's own provinces are kept, so a province taken away also takes its notifications away.
// MVVM: ViewModel helper. The bell and banner (InboxViewModel) and the Notifications screen (NotificationsViewModel) both need this
// list, so it lives here once. It reads from the repositories and has no Flutter imports.
import '../models/entities.dart';
import '../models/screen_models.dart';
import '../repositories/repository_set.dart';

class NotificationFeed {
  NotificationFeed(this._repositories);

  /// The longest list kept: older notifications stay in the database, but the inbox shows the newest ones.
  static const maxItems = 60;

  final RepositorySet _repositories;

  Future<List<NotificationItem>> load(AppUser user) async {
    final notes = await _repositories.alerts.notifications(user.uid); // newest first
    if (notes.isEmpty) return const [];
    final alerts = {for (final a in await _repositories.alerts.alerts(user.locationIds)) a.id: a};
    final names = {for (final l in await _repositories.locations.locations(user.locationIds)) l.id: l.name};
    return [
      for (final n in notes)
        if (alerts[n.alertId] case final alert?) NotificationItem(notification: n, alert: alert, locationName: names[alert.locationId] ?? alert.locationId),
    ].take(maxItems).toList();
  }

  /// Marks the given notifications read in one update.
  Future<void> markRead(AppUser user, Iterable<NotificationItem> items) =>
      _repositories.alerts.markAllRead(user.uid, [for (final i in items) if (!i.isRead) i.alertId], _repositories.clock.nowUtc());
}
