// SUMMARY: The ViewModel of M4 / M4b, Province detail: the headline soil moisture, the other parameters against their bands
// (in range, near limit, out of range), and each sensor's battery, signal, state and last communication.
// It also remembers which of the three tabs (Readings, Trends, Sensors) is open.
// MVVM: ViewModel. It refuses a province that is not assigned to the person (rule 4).
import '../models/enums.dart';
import '../models/rules.dart';
import '../models/screen_models.dart';
import '../repositories/repository_set.dart';
import 'base_view_model.dart';
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

    // The traffic light of each parameter: its current average against its band.
    final tiles = <ParameterTile>[];
    for (final p in Parameter.values) {
      final v = card.averages[p], b = bands[p];
      if (v == null || b == null) continue;
      tiles.add(ParameterTile(parameter: p, value: v, min: b.minValue, max: b.maxValue, status: BandRules.status(v, b.minValue, b.maxValue)));
    }

    // The state of each sensor (rule 2: 2 hours silent is Offline, battery below 25% is low).
    final statuses = {for (final s in await locationRepo.sensorStatuses(locationId)) s.sensorId: s};
    final rows = <SensorRow>[];
    var lastUpdated = now.subtract(const Duration(days: 365));
    for (final s in await locationRepo.sensors(locationId)) {
      final st = statuses[s.id];
      if (st != null && st.lastCommunication.isAfter(lastUpdated)) lastUpdated = st.lastCommunication;
      String label, key;
      if (!s.isActive || st == null || SensorRules.isOffline(st.lastCommunication, now)) {
        label = 'Offline';
        key = 'off';
      } else if (SensorRules.isLowBattery(st.batteryLevel)) {
        label = 'Battery low';
        key = 'warn';
      } else if (st.signal == 'Weak') {
        label = 'Weak signal';
        key = 'warn';
      } else {
        label = 'Online';
        key = 'ok';
      }
      rows.add(SensorRow(sensor: s, status: st, stateLabel: label, stateColourKey: key));
    }
    return LocationDetail(card: card, lastUpdated: lastUpdated, tiles: tiles, sensors: rows);
  }
}
