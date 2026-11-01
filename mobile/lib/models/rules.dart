// SUMMARY: The four business rules of SensorGrid written as small functions, plus the agreed assumptions (marked ASSUMPTION).
// MVVM: Model. ViewModels call these before every read or write instead of repeating the logic.
//   1. A value outside its band is a Breach.
//   2. No reading for 1 minute is an Offline alert (the gateway sends a reading about every 6 seconds).
//   3. Acknowledge and Resolve both need a note.
//   4. A Standard User sees only assigned locations (enforced in every ViewModel and, later, by Firebase Security Rules).
import 'entities.dart'; // AppUser, used by the access rule
import 'enums.dart'; // BandStatus, AlertSeverity, AlertState

// Rule 2: when a sensor counts as offline.
class SensorRules {
  /// Rule 2: this long without a reading means the sensor is offline (the same limit as the web console).
  static const Duration offlineAfter = Duration(minutes: 1);

  // true when 1 minute or more has passed since the sensor last talked to the gateway
  static bool isOffline(DateTime lastCommunicationUtc, DateTime nowUtc) => nowUtc.difference(lastCommunicationUtc) >= offlineAfter;
}

// Rule 1: a value against its allowed band (min to max).
class BandRules {
  /// Rule 1: a value outside min..max is a breach (the limits themselves are still inside).
  static bool isBreach(double value, double min, double max) => value < min || value > max;

  /// ASSUMPTION: "near limit" means within 10% of the band width from either edge.
  static const double nearLimitFraction = 0.10;

  /// The traffic light for a value: in range, near limit or out of range.
  static BandStatus status(double value, double min, double max) {
    if (isBreach(value, min, max)) return BandStatus.outOfRange; // red
    final margin = (max - min) * nearLimitFraction; // 10% of the band width
    if (value - min <= margin || max - value <= margin) return BandStatus.nearLimit; // amber
    return BandStatus.inRange; // green
  }

  /// How far outside the band a value is, as a fraction of the band width (0 when inside).
  static double distanceOutside(double value, double min, double max) {
    if (value < min) return (min - value) / (max - min); // below the band
    if (value > max) return (value - max) / (max - min); // above the band
    return 0;
  }
}

// How serious each kind of alert is.
class SeverityRules {
  /// ASSUMPTION: a breach more than 20% of the band width outside the band is Critical.
  static const double criticalFraction = 0.20;

  /// ASSUMPTION: Offline is Critical.
  static AlertSeverity forBreach(double value, double min, double max) =>
      BandRules.distanceOutside(value, min, max) > criticalFraction ? AlertSeverity.critical : AlertSeverity.warning;

  static const AlertSeverity forOffline = AlertSeverity.critical;
}

/// Thrown when a rule is broken (for example a missing note). The message is safe to show to the person.
class RuleViolation implements Exception {
  const RuleViolation(this.message);
  final String message;
  @override
  String toString() => message; // so the screen can show the message as it is
}

// Rule 3, and the order an alert's state may move in.
class AlertRules {
  /// Alert state only moves forward: Open to Acknowledged, Acknowledged to Resolved, or Open straight to Resolved.
  static bool canMove(AlertState from, AlertState to) =>
      (from == AlertState.open && to == AlertState.acknowledged) ||
      (from == AlertState.open && to == AlertState.resolved) ||
      (from == AlertState.acknowledged && to == AlertState.resolved);

  /// Rule 3: acknowledging or resolving needs a note that is not empty.
  static String requireNote(String? note) {
    final trimmed = (note ?? '').trim(); // remove spaces at the start and end
    if (trimmed.isEmpty) throw const RuleViolation('A note is required.');
    return trimmed;
  }

  static const int maxNoteLength = 200; // the longest note the app accepts
}

// Rule 4: which sites a person may see.
class AccessRules {
  /// Rule 4: a person may open a location only if it is assigned to them.
  static bool canSee(AppUser user, String locationId) => user.locationIds.contains(locationId);
}