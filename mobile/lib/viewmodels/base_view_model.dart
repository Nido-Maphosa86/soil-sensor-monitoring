// SUMMARY: The shared starting point of every screen ViewModel that loads data: it keeps the screen state
// (loading, ready or failed), the data itself and the error, and reloads when the session says data changed.
// It also keeps a screen live: a ViewModel names the changes it follows ([liveTopics]) and how often at most it reloads
// ([liveEvery]); those reloads are quiet (the old data stays on screen until the new data is in). Pull to refresh uses [reload].
// MVVM: ViewModel. A screen ViewModel only has to say HOW to fetch its data (the fetch() method); the rest is here.
// It imports foundation.dart only (for ChangeNotifier), never Flutter widgets.
import 'dart:async';

import 'package:flutter/foundation.dart';

import '../models/entities.dart';
import 'live_signals.dart';
import 'session_view_model.dart';

/// What a screen is showing right now.
enum LoadState { loading, ready, failed }

abstract class LoadingViewModel<T> extends ChangeNotifier {
  LoadingViewModel(this.session) : _seenVersion = session.dataVersion {
    session.addListener(_onSessionChanged);
    for (final t in LiveTopic.values) {
      _liveSeen[t] = session.live.version(t);
    }
    session.live.addListener(_onLive);
  }

  final SessionViewModel session;

  LoadState _state = LoadState.loading;
  T? _data;
  Object? _error;
  int _seenVersion;
  bool _disposed = false;
  bool _busy = false; // a load is running
  DateTime _loadedAt = DateTime.fromMillisecondsSinceEpoch(0);
  Timer? _pending; // a live reload waiting for liveEvery to pass
  final Map<LiveTopic, int> _liveSeen = {};

  LoadState get state => _state;

  /// The loaded data. Only read it when [state] is [LoadState.ready].
  T get data => _data as T;

  /// What went wrong when [state] is [LoadState.failed].
  Object? get error => _error;

  /// The signed-in person. Screens are only shown while someone is signed in.
  AppUser get user => session.user!;

  /// Whether this screen loads again when the session says data changed. A screen can switch it off.
  @protected
  bool get reloadOnDataChange => true;

  /// The live changes this screen follows (none unless a ViewModel says so).
  @protected
  Set<LiveTopic> get liveTopics => const {};

  /// The shortest time between two live reloads of this screen.
  @protected
  Duration get liveEvery => const Duration(seconds: 10);

  /// Each screen ViewModel says here how its data is fetched from the repositories.
  @protected
  Future<T> fetch();

  /// Loads (or reloads) the data and tells the View. A quiet load keeps the old data on screen while it runs,
  /// and keeps it if the load fails (the next one will try again).
  Future<void> load({bool quiet = false}) async {
    quiet = quiet && _state == LoadState.ready;
    if (quiet && _busy) return; // the load already running will bring the newest data
    _busy = true;
    if (!quiet) {
      _state = LoadState.loading;
      _error = null;
      _notify();
    }
    try {
      _data = await fetch();
      _state = LoadState.ready;
      _error = null;
    } catch (e) {
      if (!quiet) {
        _error = e;
        _state = LoadState.failed;
      }
    } finally {
      _busy = false;
      _loadedAt = DateTime.now();
    }
    _notify();
  }

  /// Pull to refresh: loads again without hiding what is on screen.
  Future<void> reload() => load(quiet: true);

  /// Tells the View to redraw, unless the screen is already closed.
  @protected
  void refresh() => _notify();

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  /// Reload only when the data really changed (not when, say, a notification switch was flipped).
  void _onSessionChanged() {
    if (session.user == null || session.dataVersion == _seenVersion) return;
    _seenVersion = session.dataVersion;
    if (reloadOnDataChange) load();
  }

  /// A followed live change arrived: reload quietly, at most once every [liveEvery].
  void _onLive() {
    var changed = false;
    for (final t in LiveTopic.values) {
      final v = session.live.version(t);
      if (v != _liveSeen[t]) {
        _liveSeen[t] = v;
        if (liveTopics.contains(t)) changed = true;
      }
    }
    if (!changed || _disposed || session.user == null || _state != LoadState.ready || _pending != null) return;
    final wait = liveEvery - DateTime.now().difference(_loadedAt);
    if (wait <= Duration.zero) {
      load(quiet: true);
    } else {
      _pending = Timer(wait, () {
        _pending = null;
        if (!_disposed) load(quiet: true);
      });
    }
  }

  @override
  void dispose() {
    _disposed = true;
    _pending?.cancel();
    session.removeListener(_onSessionChanged);
    session.live.removeListener(_onLive);
    super.dispose();
  }
}
