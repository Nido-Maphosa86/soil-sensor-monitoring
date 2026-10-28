// SUMMARY: The ready-to-show data each screen needs (home summary, location detail, trend, alerts overview, reports).
// MVVM: Model. ViewModels build these from the repositories; Views only draw them.
import 'entities.dart'; // Location, Sensor, AlertItem, Report...
import 'enums.dart'; // Parameter, BandStatus, TrendRange

/// The state of one sensor square in the home network grid.
enum NodeState { inRange, warning, breach, offline }

// One row of the home grid: a site and the state of each of its sensors.
class GridRow {
  const GridRow(this.locationName, this.nodes);
  final String locationName;
  final List<NodeState> nodes;
}

/// The numbers of one location card (home and sites screens).
class LocationCardData {
  const LocationCardData({
    required this.location,
    required this.sensorsReporting,
    required this.sensorsTotal,
    required this.readingsToday,
    required this.inRangePercent,
    required this.averages,
  });

  final Location location;
  final int sensorsReporting; // for example 4 of 5 reporting
  final int sensorsTotal;
  final int readingsToday;
  final int inRangePercent;

  /// The current average of each parameter across the reporting sensors.
  final Map<Parameter, double> averages;

  /// ASSUMPTION: 70% or more of readings in range is "Optimal", otherwise "Attention".
  bool get optimal => inRangePercent >= 70;
  String get statusLabel => optimal ? 'Optimal' : 'Attention';
}

// Everything the Home tab shows.
class HomeData {
  const HomeData({
    required this.nowUtc,
    required this.lastSync,
    required this.reporting,
    required this.total,
    required this.grid,
    required this.readingsToday,
    required this.openAlerts,
    required this.topAlert,
    required this.locations,
    required this.locationNames,
  });

  final DateTime nowUtc;
  final DateTime lastSync; // time of the newest reading
  final int reporting;
  final int total;
  final List<GridRow> grid;
  final int readingsToday;
  final int openAlerts;
  final AlertItem? topAlert; // the most urgent open alert, or empty when none
  final List<LocationCardData> locations;

  /// Location id to name (FS to Free State), for the alert preview.
  final Map<String, String> locationNames;
}

// One parameter tile on the province screen: value, band and traffic light.
class ParameterTile {
  const ParameterTile({required this.parameter, required this.value, required this.min, required this.max, required this.status});

  final Parameter parameter;
  final double value;
  final double min;
  final double max;
  final BandStatus status;
}

// One line in the province screen's sensor list.
class SensorRow {
  const SensorRow({required this.sensor, required this.status, required this.stateLabel, required this.stateColourKey});

  final Sensor sensor;
  final SensorStatus? status; // empty when the gateway has not reported yet

  /// Online, Offline, Battery low or Weak signal.
  final String stateLabel;

  /// One of: ok, warn, off (the screen maps this to a colour).
  final String stateColourKey;
}

// Everything the province (site) detail screen shows.
class LocationDetail {
  const LocationDetail({required this.card, required this.lastUpdated, required this.tiles, required this.sensors});

  final LocationCardData card;
  final DateTime lastUpdated;
  final List<ParameterTile> tiles;
  final List<SensorRow> sensors;

  Location get location => card.location; // shortcut to the site itself
}

// One point on a trend chart.
class TrendPoint {
  const TrendPoint(this.time, this.value);
  final DateTime time;
  final double value;
}

// Everything the trend chart screen shows.
class TrendData {
  const TrendData({
    required this.parameter,
    required this.locationName,
    required this.sensorId,
    required this.sensorIds,
    required this.range,
    required this.points,
    required this.now,
    required this.lowest,
    required this.average,
    required this.highest,
    required this.changePercent,
    required this.band,
  });

  final Parameter parameter;
  final String locationName;
  final String sensorId; // the sensor being shown
  final List<String> sensorIds; // the sensors to choose from
  final TrendRange range;
  final List<TrendPoint> points;
  final double now;
  final double lowest;
  final double average;
  final double highest;

  /// Change of the average against the period before, in percent (null when there is nothing to compare).
  final double? changePercent;
  final Threshold? band; // drawn as the shaded band behind the line
}

// The filter chips on the Alerts tab.
enum AlertFilter {
  all('All'),
  critical('Critical'),
  warning('Warning'),
  offline('Offline'),
  acknowledged('Acknowledged'),
  resolved('Resolved');

  const AlertFilter(this.label);
  final String label; // the text on the chip
}

// Everything the Alerts tab shows.
class AlertsOverview {
  const AlertsOverview({required this.open, required this.acknowledged, required this.resolved, required this.alerts, required this.nowUtc, required this.locationNames});

  final int open;
  final int acknowledged;
  final int resolved;

  /// The alerts matching the chosen filter, newest first.
  final List<AlertItem> alerts;
  final DateTime nowUtc;

  /// Location id to name, for the "Gauteng · GP-02" lines.
  final Map<String, String> locationNames;
}

// One line in an alert's activity history.
class ActivityLine {
  const ActivityLine(this.time, this.text, {this.important = false});
  final DateTime time;
  final String text;
  final bool important; // shown highlighted
}

// Everything the alert detail screen shows.
class AlertDetail {
  const AlertDetail({required this.alert, required this.locationName, required this.activity, required this.band, required this.nowUtc});

  final AlertItem alert;
  final String locationName;
  final List<ActivityLine> activity;
  final Threshold? band;
  final DateTime nowUtc;
}

// One site's average N, P and K, for the nutrient chart on the Reports tab.
class NutrientGroup {
  const NutrientGroup(this.locationId, this.locationName, this.nitrogen, this.phosphorus, this.potassium);
  final String locationId;
  final String locationName;
  final double nitrogen;
  final double phosphorus;
  final double potassium;
}

// One site's share of readings in range, for the Reports tab.
class InRangeRow {
  const InRangeRow(this.locationName, this.percent);
  final String locationName;
  final int percent;
}

// Everything the Reports tab shows.
class ReportsData {
  const ReportsData({required this.periodLabel, required this.nutrients, required this.inRange, required this.allSites, required this.reports});

  final String periodLabel;
  final List<NutrientGroup> nutrients;
  final List<InRangeRow> inRange;
  final int allSites; // the in-range percent across all sites together
  final List<Report> reports;
}
