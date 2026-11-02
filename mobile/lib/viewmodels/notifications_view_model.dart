// SUMMARY: The ViewModel of M10 Notifications (opened from the bell on Home): the person's notifications, newest first, each with
// the alert it is about. It follows new notifications and alert changes live, and offers "Mark all read".
// Opening a notification opens its alert (M7), which marks that notification read.
// MVVM: ViewModel. The list itself comes from NotificationFeed, shared with the bell and the banner.
import '../models/screen_models.dart';
import '../repositories/repositories.dart';
import '../repositories/repository_set.dart';
import 'base_view_model.dart';
import 'live_signals.dart';
import 'notification_feed.dart';

class NotificationsViewModel extends LoadingViewModel<NotificationsData> {
  NotificationsViewModel(RepositorySet repositories, super.session)
      : _feed = NotificationFeed(repositories),
        _clock = repositories.clock;

  final NotificationFeed _feed;
  final Clock _clock;

  bool _marking = false;

  /// True while "Mark all read" is being saved.
  bool get marking => _marking;

  /// A new notification, or an alert acknowledged or resolved (its state shows on the row).
  @override
  Set<LiveTopic> get liveTopics => const {LiveTopic.notifications, LiveTopic.alerts};

  @override
  Duration get liveEvery => const Duration(seconds: 2);

  @override
  Future<NotificationsData> fetch() async => NotificationsData(items: await _feed.load(user), nowUtc: _clock.nowUtc());

  /// Marks every unread notification read in one update. Returns an error message, or null when it worked.
  Future<String?> markAllRead() async {
    if (state != LoadState.ready || data.unread == 0 || _marking) return null;
    _marking = true;
    refresh();
    try {
      await _feed.markRead(user, data.items);
      await load(quiet: true);
      return null;
    } catch (_) {
      return 'Could not mark them read. Check your connection and try again.';
    } finally {
      _marking = false;
      refresh();
    }
  }
}
