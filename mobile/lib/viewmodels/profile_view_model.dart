// SUMMARY: The ViewModel of M9 Profile: the names of the assigned provinces, the three notification switches
// (threshold breaches, sensor offline, daily summary at 07:00) and Sign out.
// MVVM: ViewModel. An Administrator on the phone gets the same Standard User functions as everyone else (no admin pages on mobile).
import '../models/enums.dart';
import '../repositories/repository_set.dart';
import 'base_view_model.dart';

class ProfileViewModel extends LoadingViewModel<Map<String, String>> {
  ProfileViewModel(this._repositories, super.session);

  final RepositorySet _repositories;

  /// The label under the name. An Administrator is told that the phone gives standard functions only.
  String get roleLabel => user.role == UserRole.administrator ? 'Administrator · standard functions' : 'Standard user';

  /// The names of the person's provinces, by id (FS to Free State).
  @override
  Future<Map<String, String>> fetch() async => {for (final l in await _repositories.locations.locations(user.locationIds)) l.id: l.name};

  /// Saves one notification switch. Returns an error message, or null when it was saved.
  Future<String?> setNotification({bool? breach, bool? offline, bool? daily}) async {
    final current = user;
    try {
      final updated = await _repositories.profile.saveNotificationSettings(
        current,
        breach: breach ?? current.notifyBreach,
        offline: offline ?? current.notifyOffline,
        daily: daily ?? current.notifyDaily,
      );
      session.updated(updated);
      return null;
    } catch (e) {
      return e.toString();
    }
  }

  Future<void> signOut() => session.signOut();
}
