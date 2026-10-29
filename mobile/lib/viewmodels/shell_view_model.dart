// SUMMARY: The ViewModel of the main frame after sign in: which of the four tabs is open, and the number of open alerts
// shown as a badge on the Alerts tab.
// MVVM: ViewModel. The badge is counted again whenever the session says data changed (for example after a Resolve).
import 'package:flutter/foundation.dart';

import '../models/enums.dart';
import '../models/rules.dart';
import '../repositories/repository_set.dart';
import 'session_view_model.dart';

class ShellViewModel extends ChangeNotifier {
  ShellViewModel(this._repositories, this._session) : _seenVersion = _session.dataVersion {
    _session.addListener(_onSessionChanged);
  }

  final RepositorySet _repositories;
  final SessionViewModel _session;

  int _index = 0;
  int _openAlerts = 0;
  int _seenVersion;
  bool _disposed = false;

  /// 0 Home, 1 Sites, 2 Alerts, 3 Reports.
  int get index => _index;
  int get openAlerts => _openAlerts;

  void selectTab(int index) {
    _index = index;
    _notify();
  }

  /// Counts the open alerts of the person's own provinces (rule 4) for the badge.
  Future<void> loadBadge() async {
    final user = _session.user;
    if (user == null) return;
    try {
      final alerts = await _repositories.alerts.alerts(user.locationIds);
      _openAlerts = alerts.where((a) => AccessRules.canSee(user, a.locationId) && a.state == AlertState.open).length;
      _notify();
    } catch (_) {
      // The badge simply stays as it was when the data cannot be loaded.
    }
  }

  void _onSessionChanged() {
    if (_session.user == null || _session.dataVersion == _seenVersion) return;
    _seenVersion = _session.dataVersion;
    loadBadge();
  }

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _session.removeListener(_onSessionChanged);
    super.dispose();
  }
}
