// SUMMARY: The app-wide ViewModel: who is signed in, and a "data changed" signal.
// MVVM: ViewModel. main.dart creates ONE SessionViewModel for the whole app. The sign-in screen fills it, the profile empties it,
// and every screen ViewModel listens to its data signal: when an alert is acknowledged or resolved, Home, Sites, Alerts,
// Reports and the tab badge load their data again. It also owns [live], which follows changes made elsewhere (the gateway,
// the alert checker on the web server, colleagues) while someone is signed in, and follows the person's provinces: when an
// administrator gives or takes away a province on the web console, the app switches to the new list within seconds.
import 'package:flutter/foundation.dart';

import '../models/entities.dart';
import '../repositories/repository_set.dart';
import 'live_signals.dart';

class SessionViewModel extends ChangeNotifier {
  SessionViewModel(this._repositories) : live = LiveSignals(_repositories) {
    live.onAssignmentsChanged = _checkAssignments;
  }

  final RepositorySet _repositories;

  /// Changes arriving from Firebase while someone is signed in (alerts at once, readings about every 6 seconds).
  final LiveSignals live;

  AppUser? _user;
  int _dataVersion = 0;

  /// The signed-in person (null means nobody, so the sign-in screen is shown).
  AppUser? get user => _user;

  /// Goes up by one every time data is changed in the app. Screen ViewModels reload when it changes.
  int get dataVersion => _dataVersion;

  /// True once the app has checked, at start, whether someone was still signed in from last time.
  bool restoreTried = false;

  void signedIn(AppUser user) {
    _user = user;
    live.start(user);
    notifyListeners();
  }

  /// Replaces the person's details (for example after a notification switch was changed).
  void updated(AppUser user) {
    _user = user;
    notifyListeners();
  }

  /// Tells every listening ViewModel that data changed and should be loaded again.
  void dataChanged() {
    _dataVersion++;
    notifyListeners();
  }

  /// The provinces changed on the web console (or Firebase sent the list again): read them, and when they really differ,
  /// listen to the new provinces and let every screen load again.
  Future<void> _checkAssignments() async {
    final user = _user;
    if (user == null) return;
    try {
      final now = await _repositories.auth.assignedLocations(user.uid);
      if (_user?.uid != user.uid || listEquals(now, user.locationIds)) return; // signed out meanwhile, or nothing changed
      _user = _user!.copyWith(locationIds: now); // the newest details, in case a switch was changed meanwhile
      live.start(_user!);
      dataChanged();
    } catch (_) {
      // the list is read again on the next change or the next sign in
    }
  }

  Future<void> signOut() async {
    live.stop();
    await _repositories.auth.signOut();
    _user = null;
    notifyListeners();
  }
}
