// SUMMARY: The ViewModel of M5 Reading trend: one parameter of one sensor over 15 minutes, 24 hours, 7 days or 30 days, with its
// target band and the lowest, average and highest values. No advice text: only numbers.
// The gateway sends a reading about every 6 seconds. 15 minutes reads every reading and averages them per 10 seconds, and refreshes by
// itself; the longer ranges take ONE reading per step (5 minutes, 1 hour, 6 hours), so a week is about 170 small reads instead of
// 100,000 rows. Rows of zeros (the probe not in the soil) are left out, the same rule as the web console.
// MVVM: ViewModel. Choosing another range or another sensor loads the trend again.
import '../models/entities.dart';
import '../models/enums.dart';
import '../models/rules.dart';
import '../models/screen_models.dart';
import '../repositories/repository_set.dart';
import 'base_view_model.dart';
import 'live_signals.dart';


class TrendViewModel extends LoadingViewModel<TrendData> {
  TrendViewModel(this._repositories, super.session, {required this.locationId, required this.parameter, String? sensorId}) : _sensorId = sensorId;

  final RepositorySet _repositories;
  final String locationId;
  final Parameter parameter;

  TrendRange _range = TrendRange.minutes15;
  String? _sensorId; // null means "the first sensor of the province"; set when the province screen had a sensor chosen

  TrendRange get range => _range;

  /// Only the 15-minute chart follows new readings; the longer ones would barely change...
  @override
  Set<LiveTopic> get liveTopics => _range == TrendRange.minutes15 ? const {LiveTopic.readings} : const {};
    @override
  Duration get liveEvery => Duration.zero;
  void setRange(TrendRange value) {
    _range = value;
    load();
  }

  void setSensor(String sensorId) {
    _sensorId = sensorId;
    load();
  }

  /// The next longer range, offered when the chosen one has no readings (null after 30 days).
  TrendRange? get longer => _range.index + 1 < TrendRange.values.length ? TrendRange.values[_range.index + 1] : null;

  @override
  Future<TrendData> fetch() async {
    if (!AccessRules.canSee(user, locationId)) throw const RuleViolation('This location is not assigned to you.');
    final locationRepo = _repositories.locations;
    final readingRepo = _repositories.readings;
    final now = _repositories.clock.nowUtc();
    final range = _range;
    final loc = (await locationRepo.locations([locationId])).first;
    final sensors = (await locationRepo.sensors(locationId)).where((s) => s.isActive).toList();
    if (sensors.isEmpty) throw const RuleViolation('This province has no active sensors yet.');
    // the chosen sensor, unless it was retired meanwhile: then the first active one
    final chosen = sensors.any((s) => s.id == _sensorId) ? _sensorId! : sensors.first.id;
    final band = (await locationRepo.thresholds(locationId)).where((t) => t.parameter == parameter).cast<Threshold?>().firstWhere((_) => true, orElse: () => null);

    // the newest value and when it arrived, for "Now" (or "Last known" when the sensor is silent)
    final latest = (await readingRepo.latestPerSensor(locationId))[chosen];
    final from = now.subtract(range.span);
    final rows = range.usesEveryReading
        ? await readingRepo.readings(locationId, chosen, from, now)
        : await readingRepo.sample(locationId, chosen, from, now, range.step);
    final values = [
      for (final r in rows)
        if (r.values[parameter] != null) (r.capturedAt, r.values[parameter]!) // zeros included
    ];

    // 15 minutes: the average of each 10 seconds. Longer ranges: already one reading per step.
    final points = <TrendPoint>[];
    if (range.usesEveryReading) {
      final buckets = <int, List<double>>{};
      for (final v in values) {
        buckets.putIfAbsent(v.$1.difference(from).inMilliseconds ~/ range.step.inMilliseconds, () => []).add(v.$2);
      }
      for (final k in buckets.keys.toList()..sort()) {
        final list = buckets[k]!;
        points.add(TrendPoint(from.add(range.step * k + range.step ~/ 2), list.reduce((a, b) => a + b) / list.length));
      }
    } else {
      for (final v in values) {
        points.add(TrendPoint(v.$1, v.$2));
      }
    }

    final all = [for (final p in points) p.value];
    return TrendData(
      parameter: parameter,
      locationName: loc.name,
      sensorId: chosen,
      sensorIds: [for (final s in sensors) s.id],
      range: range,
      points: points,
      now: latest?.values[parameter],
      lastReadingAt: latest?.capturedAt,
      offline: latest == null || SensorRules.isOffline(latest.capturedAt, now),
      lowest: all.isEmpty ? null : all.reduce((a, b) => a < b ? a : b),
      average: all.isEmpty ? null : all.reduce((a, b) => a + b) / all.length,
      highest: all.isEmpty ? null : all.reduce((a, b) => a > b ? a : b),
      band: band,
    );
  }
}
