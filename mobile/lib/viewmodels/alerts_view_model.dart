// SUMMARY: The ViewModel of M6 Alerts: the counters (open, acknowledged, resolved), the chosen filter
// (All, Critical, Warning, Offline, Acknowledged, Resolved) and the matching alerts grouped by day, newest first.
// MVVM: ViewModel. Only alerts of the person's own provinces are shown (rule 4).
import '../core/format.dart';
import '../models/entities.dart';
import '../models/enums.dart';
import '../models/rules.dart';
import '../models/screen_models.dart';
import '../repositories/repository_set.dart';
import 'base_view_model.dart';

class AlertsViewModel extends LoadingViewModel<AlertsOverview> {
  AlertsViewModel(this._repositories, super.session);

  final RepositorySet _repositories;
  AlertFilter _filter = AlertFilter.all;

  AlertFilter get filter => _filter;

  void setFilter(AlertFilter value) {
    _filter = value;
    load();
  }

  /// The shown alerts grouped by day ("Today", "Yesterday", "6 Sep"), keeping the newest day first.
  Map<String, List<AlertItem>> get groups {
    final result = <String, List<AlertItem>>{};
    for (final a in data.alerts) {
      result.putIfAbsent(dayLabel(a.raisedAt, data.nowUtc), () => []).add(a);
    }
    return result;
  }

  @override
  Future<AlertsOverview> fetch() async {
    final filter = _filter;
    final mine = (await _repositories.alerts.alerts(user.locationIds)).where((a) => AccessRules.canSee(user, a.locationId)).toList();
    final shown = mine.where((a) => switch (filter) {
          AlertFilter.all => true,
          AlertFilter.critical => a.severity == AlertSeverity.critical && a.type != AlertType.offline,
          AlertFilter.warning => a.severity == AlertSeverity.warning,
          AlertFilter.offline => a.type == AlertType.offline,
          AlertFilter.acknowledged => a.state == AlertState.acknowledged,
          AlertFilter.resolved => a.state == AlertState.resolved,
        }).toList()
      ..sort((a, b) => b.raisedAt.compareTo(a.raisedAt));
    return AlertsOverview(
      open: mine.where((a) => a.state == AlertState.open).length,
      acknowledged: mine.where((a) => a.state == AlertState.acknowledged).length,
      resolved: mine.where((a) => a.state == AlertState.resolved).length,
      alerts: shown,
      nowUtc: _repositories.clock.nowUtc(),
      locationNames: {for (final l in await _repositories.locations.locations(user.locationIds)) l.id: l.name},
    );
  }
}
