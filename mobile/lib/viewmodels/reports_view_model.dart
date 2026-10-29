// SUMMARY: The ViewModel of M8 Reports: the nutrient levels of the person's provinces (last 30 days), the share of readings
// in range, and the generated reports that can be downloaded.
// MVVM: ViewModel. A report is only downloaded when ALL its provinces are assigned to the person (rule 4).
import '../core/format.dart';
import '../models/entities.dart';
import '../models/enums.dart';
import '../models/rules.dart';
import '../models/screen_models.dart';
import '../repositories/repository_set.dart';
import 'base_view_model.dart';
import 'location_insights.dart';

/// The answer to a download, for the message at the bottom of the screen.
class DownloadResult {
  const DownloadResult({required this.ok, required this.title, required this.detail});
  final bool ok;
  final String title;
  final String detail;
}

class ReportsViewModel extends LoadingViewModel<ReportsData> {
  ReportsViewModel(this._repositories, super.session) : _insights = LocationInsights(_repositories.locations, _repositories.readings, _repositories.clock);

  final RepositorySet _repositories;
  final LocationInsights _insights;

  String? _downloadingId;

  /// The id of the report being downloaded right now (null when none).
  String? get downloadingId => _downloadingId;

  DateTime get nowUtc => _repositories.clock.nowUtc();

  /// The newest generated report (for the big download button), or null when there is none.
  Report? get latest => data.reports.isEmpty ? null : (List<Report>.of(data.reports)..sort((a, b) => b.generatedAt.compareTo(a.generatedAt))).first;

  @override
  Future<ReportsData> fetch() async {
    final now = _repositories.clock.nowUtc();
    final locations = await _repositories.locations.locations(user.locationIds);
    final nutrients = <NutrientGroup>[];
    final inRange = <InRangeRow>[];
    var sum = 0, count = 0;

    // The average of nitrogen, phosphorus and potassium over the last 30 days, per province.
    for (final loc in locations) {
      final sensors = (await _repositories.locations.sensors(loc.id)).where((s) => s.isActive).toList();
      final sums = {Parameter.nitrogen: 0.0, Parameter.phosphorus: 0.0, Parameter.potassium: 0.0};
      var n = 0;
      for (final s in sensors) {
        for (final r in await _repositories.readings.readings(loc.id, s.id, now.subtract(const Duration(days: 30)), now)) {
          for (final p in sums.keys) {
            sums[p] = sums[p]! + (r.values[p] ?? 0);
          }
          n++;
        }
      }
      nutrients.add(NutrientGroup(
        loc.id,
        loc.name,
        n == 0 ? 0 : sums[Parameter.nitrogen]! / n,
        n == 0 ? 0 : sums[Parameter.phosphorus]! / n,
        n == 0 ? 0 : sums[Parameter.potassium]! / n,
      ));
      final card = await _insights.card(loc);
      inRange.add(InRangeRow(loc.name, card.inRangePercent));
      sum += card.inRangePercent;
      count++;
    }

    return ReportsData(
      periodLabel: monthYear(now),
      nutrients: nutrients,
      inRange: inRange,
      allSites: count == 0 ? 0 : (sum / count).round(),
      reports: await _repositories.reports.reports(user.locationIds),
    );
  }

  /// Downloads a report file and says how it went.
  Future<DownloadResult> download(Report report) async {
    _downloadingId = report.id;
    refresh();
    try {
      if (!report.locationIds.every((l) => AccessRules.canSee(user, l))) {
        throw const RuleViolation('This report includes locations that are not assigned to you.');
      }
      final file = await _repositories.reports.download(report);
      // CONNECT FIREBASE HERE (later): save `file.bytes` to the phone (for example with path_provider) and offer to open it.
      return DownloadResult(ok: true, title: 'Report saved', detail: file.name);
    } catch (e) {
      return DownloadResult(ok: false, title: 'Could not download the report', detail: e.toString());
    } finally {
      _downloadingId = null;
      refresh();
    }
  }
}
