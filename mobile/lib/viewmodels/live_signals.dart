// SUMMARY: Keeps the open screens up to date by themselves. While someone is signed in it listens to Firebase for:
// the alerts of their provinces (any change arrives at once), the newest reading of each of their sensors (about every 6 seconds),
// their own notifications (a new one arrives at once), and their list of provinces (an administrator gives or takes one away).
// It does not hold any data: it only counts the changes, and each screen ViewModel decides whether and how often to reload.
// A heartbeat every 20 seconds also counts as a readings change, so a sensor that goes silent turns Offline on screen after 1 minute
// even though no new reading arrives to say so.
// MVVM: ViewModel. SessionViewModel starts it after sign in, starts it again when the provinces change, and stops it at sign out.
import 'dart:async';

import 'package:flutter/foundation.dart';

import '../models/entities.dart';
import '../repositories/repository_set.dart';

/// The kinds of change a screen can follow.
enum LiveTopic { alerts, readings, notifications }

class LiveSignals extends ChangeNotifier {
  LiveSignals(this._repositories);

  /// How often the heartbeat counts as a readings change (offline is 1 minute, so 20 seconds shows it in time).
  static const heartbeat = Duration(seconds: 5);

  final RepositorySet _repositories;
  final List<StreamSubscription<void>> _subscriptions = [];
  Timer? _timer;
  int _alerts = 0;
  int _readings = 0;
  int _notifications = 0;

  /// Called when the person's provinces may have changed on the web console (SessionViewModel checks and reacts).
  VoidCallback? onAssignmentsChanged;

  /// Goes up by one each time an alert of the person's provinces changes.
  int get alertsVersion => _alerts;

  /// Goes up by one each time a sensor sends a reading, and with every heartbeat.
  int get readingsVersion => _readings;

  /// Goes up by one each time one of the person's notifications is added or marked read.
  int get notificationsVersion => _notifications;

  int version(LiveTopic topic) => switch (topic) {
        LiveTopic.alerts => _alerts,
        LiveTopic.readings => _readings,
        LiveTopic.notifications => _notifications,
      };

  /// Starts listening for the person's provinces (only those: the database rules refuse the others anyway).
  Future<void> start(AppUser user) async {
    stop();
    _subscriptions.add(_repositories.alerts.watchNotifications(user.uid).listen((_) => _bump(LiveTopic.notifications)));
    _subscriptions.add(_repositories.auth.watchAssignments(user.uid).listen((_) => onAssignmentsChanged?.call()));
    for (final loc in user.locationIds) {
      _subscriptions.add(_repositories.alerts.watchAlerts(loc).listen((_) => _bump(LiveTopic.alerts)));
      try {
        for (final s in await _repositories.locations.sensors(loc)) {
          if (s.isActive) _subscriptions.add(_repositories.readings.watchLatest(s.id).listen((_) => _bump(LiveTopic.readings)));
        }
      } catch (_) {
        // without the sensor list the readings simply refresh with the heartbeat
      }
    }
    _timer = Timer.periodic(heartbeat, (_) => _bump(LiveTopic.readings));
  }

  /// Stops every listener (at sign out).
  void stop() {
    for (final s in _subscriptions) {
      s.cancel();
    }
    _subscriptions.clear();
    _timer?.cancel();
    _timer = null;
  }

  void _bump(LiveTopic topic) {
    switch (topic) {
      case LiveTopic.alerts:
        _alerts++;
      case LiveTopic.readings:
        _readings++;
      case LiveTopic.notifications:
        _notifications++;
    }
    notifyListeners();
  }

  @override
  void dispose() {
    stop();
    super.dispose();
  }
}
