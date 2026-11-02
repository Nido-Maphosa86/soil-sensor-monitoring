// SUMMARY: The plain data objects of SensorGrid (user, location, sensor, reading, threshold, alert...).
// MVVM: Model. The field names follow the Realtime Database tree in the database design (SRS v4.0), so the Firebase code maps them one to one.
// All times are UTC.
import 'enums.dart'; // the fixed lists of choices from the first file

// A person with an account (`users/{uid}`).
class AppUser {
  const AppUser({
    required this.uid,
    required this.name,
    required this.email,
    required this.role,
    required this.status,
    required this.createdAt,
    required this.locationIds,
    this.notifyBreach = true, // the notification switches start on
    this.notifyOffline = true,
  });

  final String uid; // the Firebase Authentication user id
  final String name;
  final String email;
  final UserRole role;
  final AccountStatus status;
  final DateTime createdAt;

  /// The locations assigned to this person (`user_locations/{uid}`).
  final List<String> locationIds;
  final bool notifyBreach;
  final bool notifyOffline;

  // a copy with some notification switches or the provinces changed (used by the profile screen and the live province list)
  AppUser copyWith({bool? notifyBreach, bool? notifyOffline, List<String>? locationIds}) => AppUser(
        uid: uid,
        name: name,
        email: email,
        role: role,
        status: status,
        createdAt: createdAt,
        locationIds: locationIds ?? this.locationIds,
        notifyBreach: notifyBreach ?? this.notifyBreach, // ?? keeps the old value when no new one is given
        notifyOffline: notifyOffline ?? this.notifyOffline,
      );
}

// A province site (`locations/{location_id}`).
class Location {
  const Location({
    required this.id,
    required this.name,
    required this.description,
    required this.crop,
    required this.latitude,
    required this.longitude,
    required this.plantingDate,
  });

  final String id; // FS, GP or NW
  final String name;
  final String description;
  final String crop;
  final double latitude;
  final double longitude;
  final String plantingDate; // yyyy-MM-dd

  // the position as text, for example "-29.1210, 26.2140"
  String get coordinates => '${latitude.toStringAsFixed(4)}, ${longitude.toStringAsFixed(4)}';
}

// One soil sensor at a site (`sensors/{loc}/{sensor_id}`).
class Sensor {
  const Sensor({
    required this.locationId,
    required this.id,
    required this.name,
    required this.model,
    required this.isActive,
    required this.includeInAverage,
    required this.installationDate,
    required this.depthCm,
    required this.modbusSlaveId,
  });

  final String locationId;
  final String id; // for example FS-03
  final String name;
  final String model;
  final bool isActive;
  final bool includeInAverage; // whether its values count in the site's average
  final String installationDate;
  final int depthCm; // how deep it sits in the soil
  final int modbusSlaveId; // its address on the gateway's wire
}

/// When a sensor last reported: the timestamp of `readings/{sensor_id}/latest`.
/// The gateway sends nothing else about the sensor's health (no battery or signal).
class SensorStatus {
  const SensorStatus({
    required this.locationId,
    required this.sensorId,
    required this.lastCommunication,
  });

  final String locationId;
  final String sensorId;
  final DateTime lastCommunication; // the timestamp of the gateway's newest row
}

/// One set of seven values captured by a sensor.
class Reading {
  const Reading({required this.locationId, required this.sensorId, required this.capturedAt, required this.values});

  final String locationId;
  final String sensorId;
  final DateTime capturedAt;
  final Map<Parameter, double> values; // a value that was not measured is simply missing
}

// The allowed band of one parameter at one site (`thresholds/{loc}/{parameter}`).
class Threshold {
  const Threshold({
    required this.locationId,
    required this.parameter,
    required this.minValue,
    required this.maxValue,
    this.basis = 'Pigeon pea',
    this.updatedBy = '',
    this.updatedAt,
  });

  final String locationId;
  final Parameter parameter;
  final double minValue;
  final double maxValue;
  final String basis; // why the band is set this way
  final String updatedBy;
  final DateTime? updatedAt; // the ? means it can be empty
}

/// What a person did to an alert (`alert_actions/.../acknowledged` or `.../resolved`).
class AlertAction {
  const AlertAction({required this.userId, required this.userName, required this.note, required this.actedAt});

  final String userId;
  final String userName;
  final String note; // always required: an empty note is refused
  final DateTime actedAt;
}

// One alert (`alerts/{loc}/{alert_id}`), with what was done to it.
class AlertItem {
  const AlertItem({
    required this.locationId,
    required this.id,
    required this.sensorId,
    required this.type,
    required this.severity,
    required this.state,
    required this.raisedAt,
    this.parameter,
    this.measuredValue,
    this.thresholdValue,
    this.acknowledged,
    this.resolved,
  });

  final String locationId;
  final String id;
  final String sensorId;
  final AlertType type;
  final Parameter? parameter; // empty for Offline alerts
  final double? measuredValue;
  final double? thresholdValue;
  final AlertSeverity severity;
  final AlertState state;
  final DateTime raisedAt;
  final AlertAction? acknowledged; // empty until someone acknowledges it
  final AlertAction? resolved; // empty until someone resolves it

  // a copy after Acknowledge or Resolve, so the screen updates straight away
  AlertItem copyWith({AlertState? state, AlertAction? acknowledged, AlertAction? resolved}) => AlertItem(
        locationId: locationId,
        id: id,
        sensorId: sensorId,
        type: type,
        severity: severity,
        state: state ?? this.state,
        raisedAt: raisedAt,
        parameter: parameter,
        measuredValue: measuredValue,
        thresholdValue: thresholdValue,
        acknowledged: acknowledged ?? this.acknowledged,
        resolved: resolved ?? this.resolved,
      );
}

/// A line in a person's alert inbox (`notifications/{uid}/{alert_id}`).
class AppNotification {
  const AppNotification({required this.alertId, required this.locationId, required this.createdAt, required this.isRead});

  final String alertId;
  final String locationId;
  final DateTime createdAt;
  final bool isRead;
}
