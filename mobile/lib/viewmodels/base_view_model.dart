// SUMMARY: The shared starting point of every screen ViewModel that loads data: it keeps the screen state
// (loading, ready or failed), the data itself and the error, and reloads when the session says data changed.
// MVVM: ViewModel. A screen ViewModel only has to say HOW to fetch its data (the fetch() method); the rest is here.
// It imports foundation.dart only (for ChangeNotifier), never Flutter widgets.
import 'package:flutter/foundation.dart';

import '../models/entities.dart';
import 'session_view_model.dart';

/// What a screen is showing right now.
enum LoadState { loading, ready, failed }

abstract class LoadingViewModel<T> extends ChangeNotifier {
  LoadingViewModel(this.session) : _seenVersion = session.dataVersion {
    session.addListener(_onSessionChanged);
  }

  final SessionViewModel session;

  LoadState _state = LoadState.loading;
  T? _data;
  Object? _error;
  int _seenVersion;
  bool _disposed = false;

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

  /// Each screen ViewModel says here how its data is fetched from the repositories.
  @protected
  Future<T> fetch();

  /// Loads (or reloads) the data and tells the View.
  Future<void> load() async {
    _state = LoadState.loading;
    _error = null;
    _notify();
    try {
      _data = await fetch();
      _state = LoadState.ready;
    } catch (e) {
      _error = e;
      _state = LoadState.failed;
    }
    _notify();
  }

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

  @override
  void dispose() {
    _disposed = true;
    session.removeListener(_onSessionChanged);
    super.dispose();
  }
}
