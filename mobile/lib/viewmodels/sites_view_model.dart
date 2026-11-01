// SUMMARY: The ViewModel of M3 Sites: the person's assigned provinces with the share of readings in range,
// plus the search text and the filter (All, Optimal, Attention) that narrow the list.
// MVVM: ViewModel. Typing in the search box or choosing a filter only changes what is shown; it does not reload the data.
// The cards refresh by themselves every 30 seconds while readings arrive.
import '../models/screen_models.dart';
import '../repositories/repository_set.dart';
import 'base_view_model.dart';
import 'live_signals.dart';
import 'location_insights.dart';

enum SiteFilter { all, optimal, attention }

class SitesViewModel extends LoadingViewModel<List<LocationCardData>> {
  SitesViewModel(this._repositories, super.session) : _insights = LocationInsights(_repositories.locations, _repositories.readings, _repositories.clock);

  final RepositorySet _repositories;
  final LocationInsights _insights;

  String _query = '';
  SiteFilter _filter = SiteFilter.all;

  SiteFilter get filter => _filter;

  @override
  Set<LiveTopic> get liveTopics => const {LiveTopic.readings};

  @override
  Duration get liveEvery => const Duration(seconds: 30);

  @override
  Future<List<LocationCardData>> fetch() async {
    final locations = await _repositories.locations.locations(user.locationIds);
    return [for (final l in locations) await _insights.card(l)];
  }

  void setQuery(String value) {
    _query = value.trim();
    refresh();
  }

  void setFilter(SiteFilter value) {
    _filter = value;
    refresh();
  }

  /// All the person's provinces.
  List<LocationCardData> get all => data;

  /// The provinces that match the search text and the filter.
  List<LocationCardData> get shown => data.where((c) {
        if (_filter == SiteFilter.optimal && !(c.hasReadings && c.optimal)) return false;
        if (_filter == SiteFilter.attention && c.hasReadings && c.optimal) return false;
        return _query.isEmpty || c.location.name.toLowerCase().contains(_query.toLowerCase());
      }).toList();

  int get optimalCount => data.where((c) => c.hasReadings && c.optimal).length;
  int get attentionCount => data.length - optimalCount;
  int get sensorTotal => data.fold<int>(0, (a, c) => a + c.sensorsTotal);
  String get crops => data.map((c) => c.location.crop.toLowerCase()).toSet().join(', ');
}
