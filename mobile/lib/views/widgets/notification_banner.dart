// SUMMARY: The banner that drops in at the top of the screen when a new alert arrives while the app is open, on any screen.
// It shows what happened and where, with View (opens the alert) and Dismiss. It goes away by itself after a few seconds.
// MVVM: View. InboxViewModel decides when there is a banner; this file only draws it. main.dart places it above every page,
// so it needs the app's navigator key to open the alert.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/format.dart';
import '../../core/theme.dart';
import '../../models/enums.dart';
import '../../models/screen_models.dart';
import '../../viewmodels/inbox_view_model.dart';
import '../routes.dart';
import 'alert_text.dart';
import 'widgets.dart';

class NotificationBanner extends StatelessWidget {
  const NotificationBanner({super.key, required this.navigator});

  /// The app's navigator, used by View to open the alert.
  final GlobalKey<NavigatorState> navigator;

  @override
  Widget build(BuildContext context) {
    final inbox = context.watch<InboxViewModel>();
    final item = inbox.banner;
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 0),
        child: AnimatedSwitcher(
          duration: const Duration(milliseconds: 250),
          transitionBuilder: (child, animation) => SlideTransition(
            position: Tween(begin: const Offset(0, -1.2), end: Offset.zero).animate(CurvedAnimation(parent: animation, curve: Curves.easeOutCubic)),
            child: FadeTransition(opacity: animation, child: child),
          ),
          child: item == null ? const SizedBox(key: ValueKey('none'), width: double.infinity) : _card(context, inbox, item),
        ),
      ),
    );
  }

  Widget _card(BuildContext context, InboxViewModel inbox, NotificationItem item) {
    final a = item.alert;
    final tone = alertTone(a);
    final kind = a.type == AlertType.offline ? 'Offline' : (a.severity == AlertSeverity.critical ? 'Critical' : 'Warning');
    return Material(
      key: ValueKey(item.alertId),
      color: Sg.card,
      elevation: 8,
      shadowColor: const Color(0x400C2E24),
      borderRadius: BorderRadius.circular(16),
      clipBehavior: Clip.antiAlias,
      child: Container(
        decoration: BoxDecoration(border: Border(left: BorderSide(color: tone.fg, width: 4))),
        padding: const EdgeInsets.fromLTRB(12, 12, 8, 6),
        child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          IconTile(alertIcon(a), fg: tone.fg, bg: tone.bg, size: 36, iconSize: 18, radius: 11),
          const SizedBox(width: 11),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text('$kind · ${alertTitle(a)}', maxLines: 1, overflow: TextOverflow.ellipsis, style: sgText(14, weight: FontWeight.w700)),
              const SizedBox(height: 2),
              Text('${item.locationName} · ${a.sensorId} · ${hhmm(item.notification.createdAt)}', style: sgText(12.5, color: Sg.tx2)),
              Row(children: [
                TextButton(
                  style: TextButton.styleFrom(padding: const EdgeInsets.symmetric(horizontal: 4), minimumSize: const Size(44, 36), foregroundColor: Sg.brand),
                  onPressed: () {
                    inbox.dismissBanner();
                    navigator.currentState?.pushNamed(AppRoutes.alert, arguments: a);
                  },
                  child: Text('View', style: sgText(13.5, weight: FontWeight.w700, color: Sg.brand)),
                ),
                const SizedBox(width: 6),
                TextButton(
                  style: TextButton.styleFrom(padding: const EdgeInsets.symmetric(horizontal: 4), minimumSize: const Size(44, 36), foregroundColor: Sg.tx3),
                  onPressed: inbox.dismissBanner,
                  child: Text('Dismiss', style: sgText(13.5, weight: FontWeight.w600, color: Sg.tx3)),
                ),
              ]),
            ]),
          ),
        ]),
      ),
    );
  }
}
