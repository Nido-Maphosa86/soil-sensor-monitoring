// SUMMARY: The ViewModel of M4 / M4b, Province detail: the headline soil moisture, the other parameters against their bands
// (in range, near limit, out of range), and each sensor's state and last communication.
// While a sensor reports, the values are live and refresh by themselves (at most every 10 seconds). When every sensor is offline,
// the values are the last ones received, marked as such, so the screen is never blank.
// It also remembers which of the three tabs (Readings, Trends, Sensors) is open.
// MVVM: ViewModel. It refuses a province that is not assigned to the person (rule 4).
import '../models/enums.dart';
import '../models/rules.dart';
import '../models/screen_models.dart';
import '../repositories/repository_set.dart';
import 'base_view_model.dart';
import 'live_signals.dart';
import 'location_insights.dart';

class LocationDetailViewModel extends LoadingViewModel<LocationDetail> {
  LocationDetailViewModel(this._repositories, super.session, this.locationId)
      : _insights = LocationInsights(_repositories.locations, _repositories.readings, _repositories.clock);

  final RepositorySet _repositories;
  final LocationInsights _insights;
  final String locationId;

  int _tab = 0;

  /// 0 Readings, 1 Trends, 2 Sensors.
  int get tab => _tab;

  /// The current time, used for "updated 5 min ago" and "Last communication".
  DateTime get nowUtc => _repositories.clock.nowUtc();

  @override
  Set<LiveTopic> get liveTopics => const {LiveTopic.readings};

  void selectTab(int value) {
    _tab = value;
    refresh();
  }

  @override
  Future<LocationDetail> fetch() async {
    if (!AccessRules.canSee(user, locationId)) throw const RuleViolation('This location is not assigned to you.');
    final locationRepo = _repositories.locations;
    final now = _repositories.clock.nowUtc();
    final loc = (await locationRepo.locations([locationId])).first;
    final card = await _insights.card(loc);
    final bands = {for (final t in await locationRepo.thresholds(locationId)) t.parameter: t};
    final sensors = await locationRepo.sensors(locationId);
    final statuses = {for (final s in await locationRepo.sensorStatuses(locationId)) s.sensorId: s};

    // Live: the current averages of the reporting sensors. Every sensor offline: the average of the last values they sent.
    final live = card.sensorsReporting > 0;
    var values = card.averages;
    if (!live) {
      final latest = await _repositories.readings.latestPerSensor(locationId);
      final counted = sensors.where((s) => s.isActive && s.includeInAverage && latest[s.id] != null).map((s) => latest[s.id]!).toList();
      values = {
        for (final p in Parameter.values)
          if (counted.any((r) => r.values[p] != null))
            p: [for (final r in counted) if (r.values[p] != null) r.values[p]!].reduce((a, b) => a + b) / counted.where((r) => r.values[p] != null).length
      };
    }

    // The traffic light of each parameter: its value against its band.
    final tiles = <ParameterTile>[];
    for (final p in Parameter.values) {
      final v = values[p], b = bands[p];
      if (v == null || b == null) continue;
      tiles.add(ParameterTile(parameter: p, value: v, min: b.minValue, max: b.maxValue, status: BandRules.status(v, b.minValue, b.maxValue), lastKnown: !live));
    }

    // The state of each sensor (rule 2: 1 minute silent is Offline).
    final rows = <SensorRow>[];
    DateTime? lastUpdated;
    for (final s in sensors) {
      final st = statuses[s.id];
      if (st != null && (lastUpdated == null || st.lastCommunication.isAfter(lastUpdated))) lastUpdated = st.lastCommunication;
      String label, key;
      if (!s.isActive || st == null || SensorRules.isOffline(st.lastCommunication, now)) {
        label = 'Offline';
        key = 'off';
      } else {
        label = 'Online';
        key = 'ok';
      }
      rows.add(SensorRow(sensor: s, status: st, stateLabel: label, stateColourKey: key));
    }
    return LocationDetail(card: card, lastUpdated: lastUpdated, live: live, tiles: tiles, sensors: rows);
  }
}
