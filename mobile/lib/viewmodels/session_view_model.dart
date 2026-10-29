// SUMMARY: The app-wide ViewModel: who is signed in, and a "data changed" signal.
// MVVM: ViewModel. main.dart creates ONE SessionViewModel for the whole app. The sign-in screen fills it, the profile empties it,
// and every screen ViewModel listens to its data signal: when an alert is acknowledged or resolved, Home, Sites, Alerts,
// Reports and the tab badge load their data again.
import 'package:flutter/foundation.dart';

import '../models/entities.dart';
import '../repositories/repository_set.dart';

class SessionViewModel extends ChangeNotifier {
  SessionViewModel(this._repositories);

  final RepositorySet _repositories;

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

  Future<void> signOut() async {
    await _repositories.auth.signOut();
    _user = null;
    notifyListeners();
  }
}
