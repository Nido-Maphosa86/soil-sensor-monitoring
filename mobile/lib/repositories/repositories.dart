// SUMMARY: The "plugs" the app needs from the outside world: sign in, locations, readings, alerts, reports, the clock.
// MVVM: Repository. These are only interfaces. The Firebase implementations are in lib/repositories/firebase.
import '../models/entities.dart';

/// Wrong email or password. The message never says which part was wrong.
class SignInFailed implements Exception {
  const SignInFailed();
}

/// The account is deactivated: the person is signed out and shown "Access denied".
class AccessDenied implements Exception {
  const AccessDenied();
}

/// The account is not a field officer (Standard User). Administrators use the web console only; the database rules refuse them here too.
class WebConsoleOnly implements Exception {
  const WebConsoleOnly();
}

/// Thrown when the connection to the backend fails.
class BackendUnavailable implements Exception {
  const BackendUnavailable([this.message = 'Could not reach the server. Check your connection and try again.']);
  final String message;
  @override
  String toString() => message;
}

abstract class Clock {
  DateTime nowUtc();
}

abstract class AuthRepository {
  Future<AppUser> signIn(String email, String password);

  /// The person still signed in from last time (Firebase keeps the sign-in on the phone), or null.
  Future<AppUser?> currentUser();

  /// Always completes the same way, whether or not the email has an account.
  Future<void> sendPasswordReset(String email);
  Future<void> signOut();
}

abstract class LocationRepository {
  Future<List<Location>> locations(List<String> locationIds);
  Future<List<Sensor>> sensors(String locationId);
  Future<List<SensorStatus>> sensorStatuses(String locationId);
  Future<List<Threshold>> thresholds(String locationId);
}

abstract class ReadingRepository {
  /// The newest reading of each sensor in a location, by sensor id.
  Future<Map<String, Reading>> latestPerSensor(String locationId);

  /// All readings of one sensor between two UTC times, oldest first.
  Future<List<Reading>> readings(String locationId, String sensorId, DateTime fromUtc, DateTime toUtc);
}

/// What a person wants to do to an alert.
class AlertActionRequest {
  const AlertActionRequest({required this.alert, required this.user, required this.resolve, required this.note, required this.actedAt});

  final AlertItem alert;
  final AppUser user;
  final bool resolve; // false means Acknowledge
  final String note;
  final DateTime actedAt;
}

abstract class AlertRepository {
  Future<List<AlertItem>> alerts(List<String> locationIds);
  Future<AlertItem?> alert(String locationId, String alertId);

  /// Saves the action AND the new state together (one multi-path update in Firebase), and returns the updated alert.
  Future<AlertItem> act(AlertActionRequest request);

  Future<List<AppNotification>> notifications(String uid);
  Future<void> markRead(String uid, String alertId, DateTime readAt);
}

abstract class ProfileRepository {
  Future<AppUser> saveNotificationSettings(AppUser user, {required bool breach, required bool offline, required bool daily});
}

/// A downloaded report file.
class ReportFile {
  const ReportFile({required this.name, required this.bytes});
  final String name;
  final List<int> bytes;
}

abstract class ReportRepository {
  Future<List<Report>> reports(List<String> locationIds);

  /// Downloads the file of a report (from Cloud Storage when Firebase is connected).
  Future<ReportFile> download(Report report);
}
