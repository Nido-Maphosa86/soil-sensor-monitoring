// SUMMARY: Keeps the open screens up to date by themselves. While someone is signed in it listens to Firebase for two things:
// the alerts of their provinces (any change arrives at once) and the newest reading of each of their sensors (about every 6 seconds).
// It does not hold any data: it only counts the changes, and each screen ViewModel decides whether and how often to reload.
// A heartbeat every 20 seconds also counts as a readings change, so a sensor that goes silent turns Offline on screen after 1 minute
// even though no new reading arrives to say so.
// MVVM: ViewModel. SessionViewModel starts it after sign in and stops it at sign out.
import 'dart:async';

import 'package:flutter/foundation.dart';

import '../models/entities.dart';
import '../repositories/repository_set.dart';

/// The kinds of change a screen can follow.
enum LiveTopic { alerts, readings }

class LiveSignals extends ChangeNotifier {
  LiveSignals(this._repositories);

  /// How often the heartbeat counts as a readings change (offline is 1 minute, so 20 seconds shows it in time).
  static const heartbeat = Duration(seconds: 20);

  final RepositorySet _repositories;
  final List<StreamSubscription<void>> _subscriptions = [];
  Timer? _timer;
  int _alerts = 0;
  int _readings = 0;

  /// Goes up by one each time an alert of the person's provinces changes.
  int get alertsVersion => _alerts;

  /// Goes up by one each time a sensor sends a reading, and with every heartbeat.
  int get readingsVersion => _readings;

  int version(LiveTopic topic) => topic == LiveTopic.alerts ? _alerts : _readings;

  /// Starts listening for the person's provinces (only those: the database rules refuse the others anyway).
  Future<void> start(AppUser user) async {
    stop();
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
    if (topic == LiveTopic.alerts) {
      _alerts++;
    } else {
      _readings++;
    }
    notifyListeners();
  }

  @override
  void dispose() {
    stop();
    super.dispose();
  }
}
