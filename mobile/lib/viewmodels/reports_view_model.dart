// SUMMARY: The ViewModel of M8 Reports: the average nitrogen, phosphorus and potassium of each of the person's provinces, and the share
// of readings in range, over the last 24 hours. Report FILES are made and downloaded on the web console, not on the phone.
// The gateway sends a reading about every 6 seconds, so the 24 hours are read as ONE reading per hour per sensor (24 small reads)
// instead of about 14,400 rows. Rows of zeros (the probe not in the soil) are left out.
// MVVM: ViewModel. Only the person's assigned provinces are read (rule 4).
import '../models/enums.dart';
import '../models/screen_models.dart';
import '../repositories/repository_set.dart';
import 'base_view_model.dart';
import 'location_insights.dart';

class ReportsViewModel extends LoadingViewModel<ReportsData> {
  ReportsViewModel(this._repositories, super.session);

  /// The period the tab covers, and how far apart the readings it uses are.
  static const period = Duration(hours: 24);
  static const step = Duration(hours: 1);

  final RepositorySet _repositories;

  @override
  Future<ReportsData> fetch() async {
    final now = _repositories.clock.nowUtc();
    final locations = await _repositories.locations.locations(user.locationIds);
    final nutrients = <NutrientGroup>[];
    final inRange = <InRangeRow>[];
    var total = 0, good = 0;

    for (final loc in locations) {
      final sensors = (await _repositories.locations.sensors(loc.id)).where((s) => s.isActive).toList();
      final bands = {for (final t in await _repositories.locations.thresholds(loc.id)) t.parameter: t};
      final sampled = await Future.wait(sensors.map((s) => _repositories.readings.sample(loc.id, s.id, now.subtract(period), now, step)));
      final rows = sampled.expand((x) => x).where((r) => !LocationInsights.isEmpty(r)).toList();

      double avg(Parameter p) {
        final v = [for (final r in rows) if (r.values[p] != null) r.values[p]!];
        return v.isEmpty ? 0 : v.reduce((a, b) => a + b) / v.length;
      }

      nutrients.add(NutrientGroup(loc.id, loc.name, avg(Parameter.nitrogen), avg(Parameter.phosphorus), avg(Parameter.potassium)));
      final ok = rows.where((r) => LocationInsights.allInRange(r, bands)).length;
      inRange.add(InRangeRow(loc.name, rows.isEmpty ? null : (100 * ok / rows.length).round()));
      total += rows.length;
      good += ok;
    }

    return ReportsData(
      periodLabel: 'Last 24 hours',
      nutrients: nutrients,
      inRange: inRange,
      allSites: total == 0 ? null : (100 * good / total).round(),
    );
  }
}
