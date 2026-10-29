// SUMMARY: The ViewModel of M2 Home: the greeting time, the sensor network summary (for example 14 of 15 reporting, one square
// per sensor), the newest open alert and a card for each of the person's provinces.
// MVVM: ViewModel. Only the person's assigned provinces are read (rule 4).
import '../models/enums.dart';
import '../models/rules.dart';
import '../models/screen_models.dart';
import '../repositories/repository_set.dart';
import 'base_view_model.dart';
import 'location_insights.dart';

class HomeViewModel extends LoadingViewModel<HomeData> {
  HomeViewModel(this._repositories, super.session) : _insights = LocationInsights(_repositories.locations, _repositories.readings, _repositories.clock);

  final RepositorySet _repositories;
  final LocationInsights _insights;

  /// "Good morning," / "Good afternoon," / "Good evening," in South African time.
  String greeting(int localHour) => localHour < 12 ? 'Good morning,' : localHour < 18 ? 'Good afternoon,' : 'Good evening,';

  @override
  Future<HomeData> fetch() async {
    final locationRepo = _repositories.locations;
    final now = _repositories.clock.nowUtc();
    final locations = await locationRepo.locations(user.locationIds);
    final alerts = await _repositories.alerts.alerts(user.locationIds);
    final unresolved = alerts.where((a) => a.state != AlertState.resolved).toList();

    final cards = <LocationCardData>[];
    final grid = <GridRow>[];
    var reporting = 0, total = 0, today = 0;
    DateTime lastSync = now.subtract(const Duration(days: 365));

    for (final loc in locations) {
      final card = await _insights.card(loc);
      cards.add(card);
      reporting += card.sensorsReporting;
      total += card.sensorsTotal;
      today += card.readingsToday;

      // One square per active sensor: offline, breach (a critical alert), warning (another alert) or in range.
      final sensors = (await locationRepo.sensors(loc.id)).where((s) => s.isActive).toList();
      final statuses = {for (final s in await locationRepo.sensorStatuses(loc.id)) s.sensorId: s};
      final nodes = <NodeState>[];
      for (final s in sensors) {
        final st = statuses[s.id];
        if (st != null && st.lastCommunication.isAfter(lastSync)) lastSync = st.lastCommunication;
        if (st == null || SensorRules.isOffline(st.lastCommunication, now)) {
          nodes.add(NodeState.offline);
          continue;
        }
        final mine = unresolved.where((a) => a.sensorId == s.id && a.type != AlertType.offline).toList();
        if (mine.any((a) => a.severity == AlertSeverity.critical)) {
          nodes.add(NodeState.breach);
        } else if (mine.isNotEmpty) {
          nodes.add(NodeState.warning);
        } else {
          nodes.add(NodeState.inRange);
        }
      }
      grid.add(GridRow(loc.name, nodes));
    }

    final open = alerts.where((a) => a.state == AlertState.open).toList()..sort((a, b) => b.raisedAt.compareTo(a.raisedAt));
    return HomeData(
      nowUtc: now,
      lastSync: lastSync,
      reporting: reporting,
      total: total,
      grid: grid,
      readingsToday: today,
      openAlerts: open.length,
      topAlert: open.isEmpty ? null : open.first,
      locations: cards,
      locationNames: {for (final l in locations) l.id: l.name},
    );
  }
}
