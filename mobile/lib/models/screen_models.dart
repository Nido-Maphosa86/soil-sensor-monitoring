// SUMMARY: The ready-to-show data each screen needs (home summary, location detail, trend, alerts overview, reports, notifications).
// MVVM: Model. ViewModels build these from the repositories; Views only draw them.
import 'entities.dart'; // Location, Sensor, AlertItem...
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
    required this.readingsLastHour,
    required this.inRangePercent,
    required this.averages,
  });

  final Location location;
  final int sensorsReporting; // for example 4 of 5 reporting
  final int sensorsTotal;
  final int readingsLastHour; // rows of zeros left out

  /// The share of the last hour's readings inside every band, or null when there were none.
  final int? inRangePercent;

  /// The current average of each parameter across the reporting sensors.
  final Map<Parameter, double> averages;

  bool get hasReadings => inRangePercent != null;

  /// ASSUMPTION: 70% or more of readings in range is "Optimal", otherwise "Attention".
  bool get optimal => (inRangePercent ?? 0) >= 70;

  /// "Offline" when no sensor reports, "No data" when they report only rows of zeros, otherwise Optimal or Attention.
  String get statusLabel => sensorsReporting == 0 ? 'Offline' : !hasReadings ? 'No data' : optimal ? 'Optimal' : 'Attention';
}

// Everything the Home tab shows.
class HomeData {
  const HomeData({
    required this.nowUtc,
    required this.lastSync,
    required this.reporting,
    required this.total,
    required this.grid,
    required this.readingsLastHour,
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
  final int readingsLastHour;
  final int openAlerts;
  final AlertItem? topAlert; // the most urgent open alert, or empty when none
  final List<LocationCardData> locations;

  /// Location id to name (FS to Free State), for the alert preview.
  final Map<String, String> locationNames;
}

// One parameter tile on the province screen: value, band and traffic light.
class ParameterTile {
  const ParameterTile({required this.parameter, required this.value, required this.min, required this.max, required this.status, this.lastKnown = false});

  final Parameter parameter;
  final double value;
  final double min;
  final double max;
  final BandStatus status;

  /// True when every sensor is offline: the value is the last one received, shown in grey.
  final bool lastKnown;
}

// One line in the province screen's sensor list.
class SensorRow {
  const SensorRow({required this.sensor, required this.status, required this.stateLabel, required this.stateColourKey});

  final Sensor sensor;
  final SensorStatus? status; // empty when the gateway has not reported yet

  /// Online or Offline.
  final String stateLabel;

  /// One of: ok, warn, off (the screen maps this to a colour).
  final String stateColourKey;
}

// Everything the province (site) detail screen shows.
class LocationDetail {
  const LocationDetail({
    required this.card,
    required this.lastUpdated,
    required this.live,
    required this.tiles,
    required this.sensors,
    this.choices = const [],
    this.selected,
    this.source = '',
  });

  final LocationCardData card;

  /// When the newest reading of any sensor arrived (null when none has ever reported).
  final DateTime? lastUpdated;

  /// True while at least one sensor reports. When false, the tiles hold the last values received.
  final bool live;
  final List<ParameterTile> tiles;
  final List<SensorRow> sensors;

  /// The sensors offered by the switch after "Average"; empty when the province has fewer than two active sensors.
  final List<String> choices;

  /// The sensor whose readings are shown, or null for the average.
  final String? selected;

  /// What the values are: "Average of 2 sensors" or "FS-01 · Online · last reading 14:32".
  final String source;

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
    required this.lastReadingAt,
    required this.offline,
    required this.lowest,
    required this.average,
    required this.highest,
    required this.band,
  });

  final Parameter parameter;
  final String locationName;
  final String sensorId; // the sensor being shown
  final List<String> sensorIds; // the sensors to choose from
  final TrendRange range;
  final List<TrendPoint> points;

  /// The newest value of the sensor (null when it has never reported), and when it arrived.
  final double? now;
  final DateTime? lastReadingAt;

  /// True when the sensor has been silent for 1 minute or more.
  final bool offline;

  /// Of the points shown; null when the period has no readings.
  final double? lowest;
  final double? average;
  final double? highest;
  final Threshold? band; // drawn as the shaded band behind the line

  bool get isEmpty => points.isEmpty;
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

// One site's share of readings in range, for the Reports tab (null when there were no readings).
class InRangeRow {
  const InRangeRow(this.locationName, this.percent);
  final String locationName;
  final int? percent;
}

// Everything the Reports tab shows. Report files are made and downloaded on the web console.
class ReportsData {
  const ReportsData({required this.periodLabel, required this.nutrients, required this.inRange, required this.allSites});

  final String periodLabel;
  final List<NutrientGroup> nutrients;
  final List<InRangeRow> inRange;
  final int? allSites; // the in-range percent across all sites together (null when there were no readings)
}

/// One line of the notifications inbox: the notification, the alert it is about, and the province name.
class NotificationItem {
  const NotificationItem({required this.notification, required this.alert, required this.locationName});

  final AppNotification notification;
  final AlertItem alert;
  final String locationName;

  String get alertId => notification.alertId;
  bool get isRead => notification.isRead;
}

/// Everything the Notifications screen shows: the newest notifications first.
class NotificationsData {
  const NotificationsData({required this.items, required this.nowUtc});

  final List<NotificationItem> items;
  final DateTime nowUtc;

  int get unread => items.where((i) => !i.isRead).length;
}
