// SUMMARY: The words, icons and colours used to describe an alert, shared by Home, the alerts list and the alert detail.
// MVVM: View helper. Alert text is a plain comparison of the reading and its threshold: NO treatment advice is shown anywhere.
import 'package:flutter/material.dart';

import '../../models/entities.dart';
import '../../models/enums.dart';
import 'widgets.dart';

/// "Nitrogen below minimum", "Sensor not reporting", "Battery low".
String alertTitle(AlertItem a) {
  switch (a.type) {
    case AlertType.offline:
      return 'Sensor not reporting';
    case AlertType.battery:
      return 'Battery low';
    case AlertType.breach:
      final name = a.parameter?.shortLabel ?? 'Reading';
      final below = (a.measuredValue ?? 0) < (a.thresholdValue ?? 0);
      return '$name ${below ? 'below minimum' : 'above maximum'}';
  }
}

/// "Gauteng · GP-02" (the location name comes from the data).
String alertWhere(AlertItem a, Map<String, String> names) => '${names[a.locationId] ?? a.locationId} · ${a.sensorId}';

IconData alertIcon(AlertItem a) {
  if (a.state == AlertState.resolved) return Icons.check_circle_outline_rounded;
  return switch (a.type) {
    AlertType.offline => Icons.wifi_off_rounded,
    AlertType.battery => Icons.battery_alert_rounded,
    AlertType.breach => a.parameter == Parameter.soilMoisture ? Icons.water_drop_outlined : Icons.warning_amber_rounded,
  };
}

/// Resolved alerts are green; offline ones are grey; otherwise the severity colour.
Tone alertTone(AlertItem a) {
  if (a.state == AlertState.resolved) return Tone.ok;
  if (a.type == AlertType.offline) return Tone.off;
  return Tone.forSeverity(a.severity);
}
