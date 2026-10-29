// SUMMARY: The ViewModel of M5 Reading trend: one parameter of one sensor over 24 hours, 7 days or 30 days, with its target band,
// the lowest, average and highest values, and the change against the period before. No advice text: only numbers.
// MVVM: ViewModel. Choosing another range or another sensor loads the trend again.
import '../core/app_config.dart';
import '../models/entities.dart';
import '../models/enums.dart';
import '../models/rules.dart';
import '../models/screen_models.dart';
import '../repositories/repository_set.dart';
import 'base_view_model.dart';

class TrendViewModel extends LoadingViewModel<TrendData> {
  TrendViewModel(this._repositories, super.session, {required this.locationId, required this.parameter});

  final RepositorySet _repositories;
  final String locationId;
  final Parameter parameter;

  TrendRange _range = TrendRange.days7;
  String? _sensorId; // null means "the first sensor of the province"

  TrendRange get range => _range;

  void setRange(TrendRange value) {
    _range = value;
    load();
  }

  void setSensor(String sensorId) {
    _sensorId = sensorId;
    load();
  }

  /// "day", "week" or "month": the period the change is compared with.
  String get comparedWith => _range == TrendRange.hours24 ? 'day' : _range == TrendRange.days7 ? 'week' : 'month';

  @override
  Future<TrendData> fetch() async {
    if (!AccessRules.canSee(user, locationId)) throw const RuleViolation('This location is not assigned to you.');
    final locationRepo = _repositories.locations;
    final readingRepo = _repositories.readings;
    final now = _repositories.clock.nowUtc();
    final range = _range;
    final loc = (await locationRepo.locations([locationId])).first;
    final sensors = (await locationRepo.sensors(locationId)).where((s) => s.isActive).toList();
    final chosen = _sensorId ?? sensors.first.id;
    final band = (await locationRepo.thresholds(locationId)).where((t) => t.parameter == parameter).cast<Threshold?>().firstWhere((_) => true, orElse: () => null);

    final readings = await readingRepo.readings(locationId, chosen, now.subtract(range.span), now);
    final values = [for (final r in readings) if (r.values[parameter] != null) (r.capturedAt, r.values[parameter]!)];

    // Short ranges show every reading; 7 and 30 days show one average per South African day.
    final points = <TrendPoint>[];
    if (range == TrendRange.hours24) {
      for (final v in values) {
        points.add(TrendPoint(v.$1, v.$2));
      }
    } else {
      final byDay = <DateTime, List<double>>{};
      for (final v in values) {
        final d = v.$1.add(AppConfig.southAfricaOffset);
        byDay.putIfAbsent(DateTime.utc(d.year, d.month, d.day), () => []).add(v.$2);
      }
      final days = byDay.keys.toList()..sort();
      for (final d in days) {
        final list = byDay[d]!;
        points.add(TrendPoint(d.subtract(AppConfig.southAfricaOffset).add(const Duration(hours: 12)), list.reduce((a, b) => a + b) / list.length));
      }
    }

    final all = [for (final v in values) v.$2];
    final avg = all.isEmpty ? 0.0 : all.reduce((a, b) => a + b) / all.length;

    // The change of the average against the period before (null when there is nothing to compare).
    final before = await readingRepo.readings(locationId, chosen, now.subtract(range.span * 2), now.subtract(range.span));
    final beforeValues = [for (final r in before) if (r.values[parameter] != null) r.values[parameter]!];
    double? change;
    if (beforeValues.isNotEmpty && avg != 0) {
      final prev = beforeValues.reduce((a, b) => a + b) / beforeValues.length;
      change = prev == 0 ? null : (avg - prev) / prev * 100;
    }

    return TrendData(
      parameter: parameter,
      locationName: loc.name,
      sensorId: chosen,
      sensorIds: [for (final s in sensors) s.id],
      range: range,
      points: points,
      now: all.isEmpty ? 0 : all.last,
      lowest: all.isEmpty ? 0 : all.reduce((a, b) => a < b ? a : b),
      average: avg,
      highest: all.isEmpty ? 0 : all.reduce((a, b) => a > b ? a : b),
      changePercent: change,
      band: band,
    );
  }
}
