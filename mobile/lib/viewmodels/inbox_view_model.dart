// SUMMARY: The app-wide ViewModel of the in-app notifications: the number of unread notifications (the bell on Home) and the banner
// that drops in at the top of any screen when a new alert arrives while the app is open.
// The web server's alert checker writes a notification for each person of the province who wants that kind (the switches on the
// profile), so switching "Sensor offline" off also stops its bell count and banner. The Alerts tab still lists every alert.
// Nothing pops up while the app is closed: that would need push notifications, which this app does not use.
// MVVM: ViewModel. main.dart creates ONE for the whole app; it follows the session (sign in, sign out, province changes)
// and the live notifications signal, so it needs no timer of its own.
import 'dart:async';

import 'package:flutter/foundation.dart';

import '../models/screen_models.dart';
import '../repositories/repository_set.dart';
import 'notification_feed.dart';
import 'session_view_model.dart';

class InboxViewModel extends ChangeNotifier {
  InboxViewModel(RepositorySet repositories, this._session) : _feed = NotificationFeed(repositories) {
    _session.addListener(_onSession);
    _session.live.addListener(_onLive);
  }

  /// How long the banner stays when nobody taps it.
  static const bannerTime = Duration(seconds: 6);

  final NotificationFeed _feed;
  final SessionViewModel _session;

  int _unread = 0;
  NotificationItem? _banner;
  Set<String>? _known; // the notifications already seen; null until the first load, which never shows a banner
  String? _uid;
  int _seenData = 0;
  int _seenNotes = 0;
  bool _busy = false;
  bool _again = false;
  bool _disposed = false;
  Timer? _hide;

  /// Unread notifications, for the bell on Home.
  int get unread => _unread;

  /// The newest notification that arrived while the app was open, shown as a banner (null when there is none).
  NotificationItem? get banner => _banner;

  void dismissBanner() {
    _hide?.cancel();
    if (_banner == null) return;
    _banner = null;
    _notify();
  }

  /// Signed in, signed out, another person, or the provinces changed.
  void _onSession() {
    final user = _session.user;
    if (user == null) {
      if (_uid == null) return;
      _uid = null;
      _known = null;
      _unread = 0;
      _hide?.cancel();
      _banner = null;
      _notify();
      return;
    }
    if (user.uid != _uid) {
      _uid = user.uid;
      _known = null; // a new sign in: what is already there is not "new"
      _seenData = _session.dataVersion;
      _seenNotes = _session.live.notificationsVersion;
      load();
    } else if (_session.dataVersion != _seenData) {
      _seenData = _session.dataVersion;
      load();
    }
  }

  /// A notification was added or marked read (here, on another phone or on the web console).
  void _onLive() {
    if (_session.user == null || _session.live.notificationsVersion == _seenNotes) return;
    _seenNotes = _session.live.notificationsVersion;
    load();
  }

  /// Counts the unread notifications and shows a banner for one that is new since the last load.
  Future<void> load() async {
    final user = _session.user;
    if (user == null) return;
    if (_busy) {
      _again = true; // one more load when this one ends, so the newest change is never missed
      return;
    }
    _busy = true;
    try {
      final items = await _feed.load(user);
      if (_session.user?.uid != user.uid) return; // signed out meanwhile
      _unread = items.where((i) => !i.isRead).length;
      final known = _known;
      if (known != null) {
        final fresh = items.where((i) => !i.isRead && !known.contains(i.alertId)).toList();
        if (fresh.isNotEmpty) _show(fresh.first); // the list is newest first
      }
      _known = {for (final i in items) i.alertId};
      _notify();
    } catch (_) {
      // the count stays as it was; the next change loads again
    } finally {
      _busy = false;
      if (_again) {
        _again = false;
        load();
      }
    }
  }

  void _show(NotificationItem item) {
    _banner = item;
    _hide?.cancel();
    _hide = Timer(bannerTime, dismissBanner);
  }

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _hide?.cancel();
    _session.removeListener(_onSession);
    _session.live.removeListener(_onLive);
    super.dispose();
  }
}
