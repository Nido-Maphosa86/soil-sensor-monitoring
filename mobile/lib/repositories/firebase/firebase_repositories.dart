// SUMMARY: The Firebase versions of every repository: Firebase Authentication for sign-in and the Realtime Database for the data.
// MVVM: Repository. Paths and field names follow the Realtime Database tree shared with the web console (see web/README.md).
// The app holds NO service key: it uses the Firebase SDK as the signed in person, so the Security Rules in web/firebase/database.rules.json
// decide what it may read and write. Those rules only let field officers (Standard Users) in, and only for their assigned provinces.
// CONNECT FIREBASE HERE: nothing to change in this file. The connection settings are in lib/core/app_config.dart.
import 'package:firebase_auth/firebase_auth.dart';
import 'package:firebase_database/firebase_database.dart';

import '../../models/entities.dart';
import '../../models/enums.dart';
import '../repositories.dart';

/// The real clock in UTC (the stored times are UTC milliseconds).
class SystemClock implements Clock {
  @override
  DateTime nowUtc() => DateTime.now().toUtc();
}

/// The two Firebase services, shared by every repository.
class FirebaseBackend {
  FirebaseBackend(this.auth, this.db);

  final FirebaseAuth auth;
  final FirebaseDatabase db;

  /// Reads one path. A refusal by the Security Rules or a network failure becomes a [BackendUnavailable] with a plain message.
  Future<Object?> read(String path) => _guard(() async => (await db.ref(path).get()).value);

  /// Reads the children of a path ordered by key, optionally a key range and only the last few (like the web console's key queries).
  Future<List<(String, Object?)>> readByKey(String path, {String? startAt, String? endAt, int? limitToLast}) => _guard(() async {
        Query q = db.ref(path).orderByKey();
        if (startAt != null) q = q.startAt(startAt);
        if (endAt != null) q = q.endAt(endAt);
        if (limitToLast != null) q = q.limitToLast(limitToLast);
        final snap = await q.get();
        return [for (final c in snap.children) (c.key!, c.value)];
      });

  /// Writes several paths in ONE update: either all of them are saved or none.
  Future<void> update(Map<String, Object?> values) => _guard(() => db.ref().update(values));

  static Future<T> _guard<T>(Future<T> Function() action) async {
    try {
      return await action();
    } on FirebaseException catch (e) {
      if (_isPermission(e)) throw const BackendUnavailable('You do not have access to this. Ask your administrator to check your provinces.');
      throw const BackendUnavailable();
    }
  }

  static bool _isPermission(FirebaseException e) => e.code.contains('permission') || (e.message ?? '').toLowerCase().contains('permission');
}

// ------------------------------------------------------------------ small helpers to read the JSON tree

Map<String, Object?> _map(Object? v) => v is Map ? {for (final e in v.entries) e.key.toString(): e.value} : const {};

String _str(Map<String, Object?> m, String k, [String fallback = '']) => m[k] is String ? m[k] as String : fallback;

double? _num(Map<String, Object?> m, String k) => m[k] is num ? (m[k] as num).toDouble() : null;

int _int(Map<String, Object?> m, String k, [int fallback = 0]) => m[k] is num ? (m[k] as num).toInt() : fallback;

bool _bool(Map<String, Object?> m, String k, [bool fallback = false]) => m[k] is bool ? m[k] as bool : fallback;

DateTime? _time(Map<String, Object?> m, String k) => m[k] is num ? DateTime.fromMillisecondsSinceEpoch((m[k] as num).toInt(), isUtc: true) : null;

int _ms(DateTime t) => t.toUtc().millisecondsSinceEpoch;

/// Reading and status keys are the capture time as yyyyMMddHHmm in UTC, so a period is a key range.
String timeKey(DateTime t) {
  final u = t.toUtc();
  String two(int n) => n.toString().padLeft(2, '0');
  return '${u.year}${two(u.month)}${two(u.day)}${two(u.hour)}${two(u.minute)}';
}

// ------------------------------------------------------------------ sign in

class FirebaseAuthRepository implements AuthRepository {
  FirebaseAuthRepository(this._b);
  final FirebaseBackend _b;

  @override
  Future<AppUser> signIn(String email, String password) async {
    final UserCredential cred;
    try {
      cred = await _b.auth.signInWithEmailAndPassword(email: email, password: password);
    } on FirebaseAuthException catch (e) {
      if (e.code == 'user-disabled') throw const AccessDenied(); // deactivated on the web console
      if (e.code == 'network-request-failed' || e.code == 'too-many-requests') throw const BackendUnavailable();
      throw const SignInFailed(); // wrong email, wrong password, unknown account: always the same answer
    }
    return _loadProfile(cred.user!.uid, firstSignIn: true);
  }

  @override
  Future<AppUser?> currentUser() async {
    final user = _b.auth.currentUser;
    if (user == null) return null;
    try {
      await user.reload(); // a deactivated (disabled) account fails here
    } on FirebaseAuthException catch (e) {
      await _b.auth.signOut();
      if (e.code == 'user-disabled') throw const AccessDenied();
      return null;
    }
    return _loadProfile(user.uid, firstSignIn: false);
  }

  /// Reads users/{uid} and user_locations/{uid}. The rules refuse administrators here, which the app shows as "web console only".
  /// The first sign in of an invited field officer finishes the invitation (Invited becomes Active).
  Future<AppUser> _loadProfile(String uid, {required bool firstSignIn}) async {
    final Map<String, Object?> u;
    try {
      u = _map((await _b.db.ref('users/$uid').get()).value);
    } on FirebaseException catch (e) {
      await _b.auth.signOut();
      if (FirebaseBackend._isPermission(e)) throw const WebConsoleOnly();
      throw const BackendUnavailable();
    }
    if (u.isEmpty) {
      await _b.auth.signOut();
      throw const SignInFailed(); // a sign-in account without a SensorGrid record
    }

    var status = AccountStatus.fromStored(_str(u, 'account_status'));
    final role = UserRole.fromStored(_str(u, 'role'));
    if (role == UserRole.standardUser && status == AccountStatus.invited) {
      await _b.update({'users/$uid/account_status': AccountStatus.active.stored});
      status = AccountStatus.active;
    }
    if (role == UserRole.standardUser && status == AccountStatus.active && firstSignIn) {
      await _b.update({'users/$uid/last_active_at': _ms(DateTime.now())});
    }

    final assigned = _map(await _b.read('user_locations/$uid'));
    return AppUser(
      uid: uid,
      name: _str(u, 'name', _b.auth.currentUser?.email ?? ''),
      email: _str(u, 'email', _b.auth.currentUser?.email ?? ''),
      role: role,
      status: status,
      createdAt: _time(u, 'created_at') ?? DateTime.now().toUtc(),
      locationIds: [for (final e in assigned.entries) if (e.value == true) e.key]..sort(),
      notifyBreach: _bool(u, 'notify_breach', true),
      notifyOffline: _bool(u, 'notify_offline', true),
      notifyDaily: _bool(u, 'notify_daily'),
    );
  }

  @override
  Future<void> sendPasswordReset(String email) async {
    try {
      await _b.auth.sendPasswordResetEmail(email: email);
    } on FirebaseAuthException {
      // ignored on purpose: the screen shows the same message whether or not the email has an account
    }
  }

  @override
  Future<void> signOut() => _b.auth.signOut();
}

// ------------------------------------------------------------------ locations, sensors, thresholds

class FirebaseLocationRepository implements LocationRepository {
  FirebaseLocationRepository(this._b);
  final FirebaseBackend _b;

  @override
  Future<List<Location>> locations(List<String> locationIds) async {
    final result = <Location>[];
    for (final id in locationIds) {
      final m = _map(await _b.read('locations/$id'));
      if (m.isEmpty) continue;
      result.add(Location(
        id: id,
        name: _str(m, 'name', id),
        description: _str(m, 'description'),
        crop: _str(m, 'crop', 'Pigeon pea'),
        latitude: _num(m, 'latitude') ?? 0,
        longitude: _num(m, 'longitude') ?? 0,
        plantingDate: _str(m, 'planting_date'),
      ));
    }
    return result;
  }

  @override
  Future<List<Sensor>> sensors(String locationId) async {
    final all = _map(await _b.read('sensors/$locationId'));
    return [
      for (final e in all.entries)
        Sensor(
          locationId: locationId,
          id: e.key,
          name: _str(_map(e.value), 'name', e.key),
          model: _str(_map(e.value), 'model'),
          isActive: _bool(_map(e.value), 'is_active', true),
          includeInAverage: _bool(_map(e.value), 'include_in_average', true),
          installationDate: _str(_map(e.value), 'installation_date'),
          depthCm: _int(_map(e.value), 'depth_cm'),
          modbusSlaveId: _int(_map(e.value), 'modbus_slave_id'),
          firmwareVersion: _str(_map(e.value), 'firmware_version'),
        ),
    ]..sort((a, b) => a.id.compareTo(b.id));
  }

  @override
  Future<List<SensorStatus>> sensorStatuses(String locationId) async {
    final result = <SensorStatus>[];
    for (final s in await sensors(locationId)) {
      // The newest status is the LAST key, so only one child is read.
      final last = await _b.readByKey('sensor_status/$locationId/${s.id}', limitToLast: 1);
      if (last.isEmpty) continue;
      final m = _map(last.first.$2);
      result.add(SensorStatus(
        locationId: locationId,
        sensorId: s.id,
        batteryLevel: _int(m, 'battery_level'),
        powerStatus: _str(m, 'power_status'),
        signal: _str(m, 'signal', 'None'),
        status: _str(m, 'status', 'online'),
        lastCommunication: _time(m, 'last_communication') ?? DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
        recordedAt: _time(m, 'recorded_at') ?? DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
      ));
    }
    return result;
  }

  @override
  Future<List<Threshold>> thresholds(String locationId) async {
    final all = _map(await _b.read('thresholds/$locationId'));
    final result = <Threshold>[];
    for (final e in all.entries) {
      final p = Parameter.fromKey(e.key);
      final m = _map(e.value);
      final min = _num(m, 'min_value'), max = _num(m, 'max_value');
      if (p == null || min == null || max == null) continue;
      result.add(Threshold(
          locationId: locationId, parameter: p, minValue: min, maxValue: max,
          basis: _str(m, 'basis'), updatedBy: _str(m, 'updated_by'), updatedAt: _time(m, 'updated_at')));
    }
    return result;
  }
}

// ------------------------------------------------------------------ readings

class FirebaseReadingRepository implements ReadingRepository {
  FirebaseReadingRepository(this._b, this._locations);
  final FirebaseBackend _b;
  final FirebaseLocationRepository _locations;

  @override
  Future<Map<String, Reading>> latestPerSensor(String locationId) async {
    final result = <String, Reading>{};
    for (final s in await _locations.sensors(locationId)) {
      final last = await _b.readByKey('readings/$locationId/${s.id}', limitToLast: 1); // the newest reading is the last key
      if (last.isNotEmpty) result[s.id] = _reading(locationId, s.id, last.first.$1, last.first.$2);
    }
    return result;
  }

  @override
  Future<List<Reading>> readings(String locationId, String sensorId, DateTime fromUtc, DateTime toUtc) async {
    // A period is a key range, so only the readings inside it are downloaded.
    final rows = await _b.readByKey('readings/$locationId/$sensorId', startAt: timeKey(fromUtc), endAt: timeKey(toUtc));
    return [for (final r in rows) _reading(locationId, sensorId, r.$1, r.$2)];
  }

  static Reading _reading(String locationId, String sensorId, String key, Object? value) {
    final m = _map(value);
    final values = <Parameter, double>{};
    for (final p in Parameter.values) {
      final v = _num(m, p.key);
      if (v != null) values[p] = v; // an empty value means no reading, which is different from zero
    }
    return Reading(locationId: locationId, sensorId: sensorId, capturedAt: _time(m, 'captured_at') ?? _fromKey(key), values: values);
  }

  static DateTime _fromKey(String k) => k.length == 12
      ? DateTime.utc(int.parse(k.substring(0, 4)), int.parse(k.substring(4, 6)), int.parse(k.substring(6, 8)), int.parse(k.substring(8, 10)), int.parse(k.substring(10, 12)))
      : DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
}

// ------------------------------------------------------------------ alerts and notifications

class FirebaseAlertRepository implements AlertRepository {
  FirebaseAlertRepository(this._b);
  final FirebaseBackend _b;

  @override
  Future<List<AlertItem>> alerts(List<String> locationIds) async {
    final result = <AlertItem>[];
    for (final loc in locationIds) {
      final all = _map(await _b.read('alerts/$loc'));
      if (all.isEmpty) continue;
      final actions = _map(await _b.read('alert_actions/$loc'));
      for (final e in all.entries) {
        result.add(_alert(loc, e.key, _map(e.value), _map(actions[e.key])));
      }
    }
    result.sort((a, b) => b.raisedAt.compareTo(a.raisedAt));
    return result;
  }

  @override
  Future<AlertItem?> alert(String locationId, String alertId) async {
    final m = _map(await _b.read('alerts/$locationId/$alertId'));
    if (m.isEmpty) return null;
    return _alert(locationId, alertId, m, _map(await _b.read('alert_actions/$locationId/$alertId')));
  }

  /// Saves the action AND the new state in ONE multi-path update, as the rules require.
  @override
  Future<AlertItem> act(AlertActionRequest request) async {
    final a = request.alert;
    final kind = request.resolve ? 'resolved' : 'acknowledged';
    final newState = request.resolve ? AlertState.resolved : AlertState.acknowledged;
    await _b.update({
      'alert_actions/${a.locationId}/${a.id}/$kind': {'user_id': request.user.uid, 'note': request.note, 'acted_at': _ms(request.actedAt)},
      'alerts/${a.locationId}/${a.id}/state': newState.stored,
    });
    final action = AlertAction(userId: request.user.uid, userName: request.user.name, note: request.note, actedAt: request.actedAt);
    return request.resolve ? a.copyWith(state: newState, resolved: action) : a.copyWith(state: newState, acknowledged: action);
  }

  @override
  Future<List<AppNotification>> notifications(String uid) async {
    final all = _map(await _b.read('notifications/$uid'));
    return [
      for (final e in all.entries)
        AppNotification(
          alertId: e.key,
          locationId: _str(_map(e.value), 'location_id'),
          createdAt: _time(_map(e.value), 'created_at') ?? DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
          isRead: _bool(_map(e.value), 'is_read'),
        ),
    ]..sort((a, b) => b.createdAt.compareTo(a.createdAt));
  }

  @override
  Future<void> markRead(String uid, String alertId, DateTime readAt) =>
      _b.update({'notifications/$uid/$alertId/is_read': true, 'notifications/$uid/$alertId/read_at': _ms(readAt)});

  AlertItem _alert(String loc, String id, Map<String, Object?> m, Map<String, Object?> actions) => AlertItem(
        locationId: loc,
        id: id,
        sensorId: _str(m, 'sensor_id'),
        type: AlertType.fromStored(_str(m, 'alert_type')),
        parameter: Parameter.fromKey(m['parameter'] as String?),
        measuredValue: _num(m, 'measured_value'),
        thresholdValue: _num(m, 'threshold_value'),
        severity: AlertSeverity.fromStored(_str(m, 'severity')),
        state: AlertState.fromStored(_str(m, 'state')),
        raisedAt: _time(m, 'raised_at') ?? DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
        acknowledged: _action(_map(actions['acknowledged'])),
        resolved: _action(_map(actions['resolved'])),
      );

  /// Field officers cannot read other people's accounts, so a colleague's action shows "You" or "A colleague" instead of a name.
  AlertAction? _action(Map<String, Object?> m) {
    if (m.isEmpty) return null;
    final uid = _str(m, 'user_id');
    return AlertAction(
      userId: uid,
      userName: uid == _b.auth.currentUser?.uid ? 'You' : 'A colleague',
      note: _str(m, 'note'),
      actedAt: _time(m, 'acted_at') ?? DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
    );
  }
}

// ------------------------------------------------------------------ profile and reports

class FirebaseProfileRepository implements ProfileRepository {
  FirebaseProfileRepository(this._b);
  final FirebaseBackend _b;

  @override
  Future<AppUser> saveNotificationSettings(AppUser user, {required bool breach, required bool offline, required bool daily}) async {
    await _b.update({
      'users/${user.uid}/notify_breach': breach,
      'users/${user.uid}/notify_offline': offline,
      'users/${user.uid}/notify_daily': daily,
    });
    return user.copyWith(notifyBreach: breach, notifyOffline: offline, notifyDaily: daily);
  }
}

/// Report FILES are generated and downloaded on the web console. The database rules cannot check that every province of a report
/// is assigned to the person, so the phone never reads the reports list; the Reports tab shows summaries worked out from readings.
class FirebaseReportRepository implements ReportRepository {
  @override
  Future<List<Report>> reports(List<String> locationIds) async => const [];

  @override
  Future<ReportFile> download(Report report) => throw const BackendUnavailable('Report files are downloaded from the SensorGrid web console.');
}
