// SUMMARY: The shared maths behind every province card: how many sensors report, the share of readings in range over the
// last 24 hours, the readings captured today, and the current average of each parameter.
// MVVM: ViewModel helper. Home, Sites, Province detail and Reports all need these numbers, so they live here once
// instead of being copied into four ViewModels. It reads from the repositories and has no Flutter imports.
import '../core/app_config.dart';
import '../models/entities.dart';
import '../models/enums.dart';
import '../models/rules.dart';
import '../models/screen_models.dart';
import '../repositories/repositories.dart';

/// Shared maths for location cards (reporting sensors, share of readings in range, current averages).
class LocationInsights {
  LocationInsights(this._locations, this._readings, this._clock);

  final LocationRepository _locations;
  final ReadingRepository _readings;
  final Clock _clock;

  /// True when all seven values of the reading sit inside their bands.
  static bool allInRange(Reading r, Map<Parameter, Threshold> bands) {
    for (final p in Parameter.values) {
      final b = bands[p];
      final v = r.values[p];
      if (b != null && v != null && BandRules.isBreach(v, b.minValue, b.maxValue)) return false;
    }
    return true;
  }

  Future<LocationCardData> card(Location loc) async {
    final now = _clock.nowUtc();
    final sensors = (await _locations.sensors(loc.id)).where((s) => s.isActive).toList();
    final statuses = {for (final s in await _locations.sensorStatuses(loc.id)) s.sensorId: s};
    final bands = {for (final t in await _locations.thresholds(loc.id)) t.parameter: t};
    final latest = await _readings.latestPerSensor(loc.id);

    final reportingIds = <String>{
      for (final s in sensors)
        if (statuses[s.id] != null && !SensorRules.isOffline(statuses[s.id]!.lastCommunication, now)) s.id
    };

    // Readings of the last 24 hours, to count the share inside the bands and the readings captured today.
    final localNow = now.add(AppConfig.southAfricaOffset);
    final midnightUtc = DateTime.utc(localNow.year, localNow.month, localNow.day).subtract(AppConfig.southAfricaOffset);
    var total = 0, good = 0, today = 0;
    for (final s in sensors) {
      for (final r in await _readings.readings(loc.id, s.id, now.subtract(const Duration(hours: 24)), now)) {
        total++;
        if (allInRange(r, bands)) good++;
        if (!r.capturedAt.isBefore(midnightUtc)) today++;
      }
    }

    // Current average of each parameter across the sensors that report and count towards the average.
    final averages = <Parameter, double>{};
    final counted = sensors.where((s) => s.includeInAverage && reportingIds.contains(s.id)).toList();
    for (final p in Parameter.values) {
      final values = [for (final s in counted) if (latest[s.id]?.values[p] != null) latest[s.id]!.values[p]!];
      if (values.isNotEmpty) averages[p] = values.reduce((a, b) => a + b) / values.length;
    }

    return LocationCardData(
      location: loc,
      sensorsReporting: reportingIds.length,
      sensorsTotal: sensors.length,
      readingsToday: today,
      inRangePercent: total == 0 ? 0 : (100 * good / total).round(),
      averages: averages,
    );
  }
}
