// SUMMARY: The ViewModel of M7 Alert detail and M7b, the note sheet. It loads the alert with its reading against the threshold
// (a plain comparison, NO treatment advice) and its activity, and it does Acknowledge and Resolve.
// MVVM: ViewModel. It checks the rules BEFORE writing:
//   rule 3: a note is required (and at most 200 characters);
//   alert state only moves forward (Open > Acknowledged > Resolved, or Open > Resolved);
//   rule 4: only alerts of the person's own provinces.
// After a successful action it tells the session that data changed, so the list, Home and the badge reload.
import '../models/entities.dart';
import '../models/enums.dart';
import '../models/rules.dart';
import '../models/screen_models.dart';
import '../repositories/repositories.dart';
import '../repositories/repository_set.dart';
import 'base_view_model.dart';

class AlertDetailViewModel extends LoadingViewModel<AlertDetail> {
  AlertDetailViewModel(this._repositories, super.session, {required this.locationId, required this.alertId});

  final RepositorySet _repositories;
  final String locationId;
  final String alertId;

  bool _acting = false;
  String? _noteError;

  /// True while an Acknowledge or Resolve is being saved.
  bool get acting => _acting;

  /// The message under the note box (null when there is nothing wrong).
  String? get noteError => _noteError;

  /// The detail page closes after an action, so it does not need to reload when data changes.
  @override
  bool get reloadOnDataChange => false;

  /// The longest note allowed.
  int get maxNoteLength => AlertRules.maxNoteLength;

  void clearNoteError() {
    if (_noteError == null) return;
    _noteError = null;
    refresh();
  }

  @override
  Future<AlertDetail> fetch() async {
    if (!AccessRules.canSee(user, locationId)) throw const RuleViolation('This alert is not in your locations.');
    final alert = await _repositories.alerts.alert(locationId, alertId);
    if (alert == null) throw const RuleViolation('That alert was not found.');
    final loc = (await _repositories.locations.locations([locationId])).first;
    final band = alert.parameter == null
        ? null
        : (await _repositories.locations.thresholds(locationId)).where((t) => t.parameter == alert.parameter).cast<Threshold?>().firstWhere((_) => true, orElse: () => null);

    final activity = <ActivityLine>[
      ActivityLine(alert.raisedAt, 'Alert raised automatically', important: true),
      ActivityLine(alert.raisedAt.add(const Duration(minutes: 1)), 'Assigned users notified'),
      if (alert.acknowledged != null) ActivityLine(alert.acknowledged!.actedAt, 'Acknowledged by ${alert.acknowledged!.userName}: ${alert.acknowledged!.note}'),
      if (alert.resolved != null) ActivityLine(alert.resolved!.actedAt, 'Resolved by ${alert.resolved!.userName}: ${alert.resolved!.note}'),
    ];

    // Opening the alert marks its notification as read (best effort).
    try {
      await _repositories.alerts.markRead(user.uid, alertId, _repositories.clock.nowUtc());
    } catch (_) {}

    return AlertDetail(alert: alert, locationName: loc.name, activity: activity, band: band, nowUtc: _repositories.clock.nowUtc());
  }

  /// Acknowledge (resolve = false) or Resolve (resolve = true) with a note. Returns true when it was saved.
  Future<bool> act({required bool resolve, required String note}) async {
    final alert = data.alert;
    _acting = true;
    _noteError = null;
    refresh();
    try {
      if (!AccessRules.canSee(user, alert.locationId)) throw const RuleViolation('This alert is not in your locations.');
      final cleanNote = AlertRules.requireNote(note); // rule 3
      if (cleanNote.length > AlertRules.maxNoteLength) throw const RuleViolation('The note can be at most ${AlertRules.maxNoteLength} characters.');
      final target = resolve ? AlertState.resolved : AlertState.acknowledged;
      if (!AlertRules.canMove(alert.state, target)) throw RuleViolation('This alert is already ${alert.state.stored.toLowerCase()}.');

      // ONE write: the action and the new state together (a multi-path update in Firebase).
      await _repositories.alerts.act(AlertActionRequest(alert: alert, user: user, resolve: resolve, note: cleanNote, actedAt: _repositories.clock.nowUtc()));
      _acting = false;
      session.dataChanged();
      return true;
    } on RuleViolation catch (e) {
      _noteError = e.message;
    } catch (e) {
      _noteError = e.toString();
    }
    _acting = false;
    refresh();
    return false;
  }
}
