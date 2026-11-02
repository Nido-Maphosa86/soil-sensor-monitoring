// SUMMARY: Screen M10, Notifications (opened from the bell on Home): the person's notifications grouped by day, newest first.
// Unread ones have a green dot and a light tint; each row shows what the alert is, where, when, and its state now.
// Tapping a row opens the alert (M7), which marks it read. "Mark all read" clears every dot at once.
// MVVM: View. NotificationsViewModel loads the list and marks it read; this file only draws it.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/format.dart';
import '../../core/theme.dart';
import '../../models/screen_models.dart';
import '../../viewmodels/notifications_view_model.dart';
import '../../viewmodels/view_model_factory.dart';
import '../routes.dart';
import '../widgets/alert_text.dart';
import '../widgets/widgets.dart';

class NotificationsScreen extends StatelessWidget {
  const NotificationsScreen({super.key});

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
        create: (c) => c.read<ViewModelFactory>().notifications()..load(),
        child: Consumer<NotificationsViewModel>(
          builder: (context, vm, _) => Scaffold(
            body: SafeArea(
              child: RefreshIndicator(
                color: Sg.brand,
                onRefresh: vm.reload,
                child: ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(20, 6, 20, 32),
                  children: [
                    ViewModelBody<NotificationsData>(viewModel: vm, builder: (context, d) => _content(context, vm, d)),
                  ],
                ),
              ),
            ),
          ),
        ),
      );

  static Widget _content(BuildContext context, NotificationsViewModel vm, NotificationsData d) {
    // newest first, grouped under "Today", "Yesterday", "5 November"
    final groups = <String, List<NotificationItem>>{};
    for (final i in d.items) {
      groups.putIfAbsent(dayLabel(i.notification.createdAt, d.nowUtc), () => []).add(i);
    }
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Row(children: [
        RoundIconButton(Icons.chevron_left_rounded, onTap: () => Navigator.of(context).pop()),
        const SizedBox(width: 14),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text('Notifications', style: sgText(20, weight: FontWeight.w700, spacing: -0.4)),
            const SizedBox(height: 1),
            Text(d.unread == 0 ? 'All read' : '${d.unread} unread', style: sgText(13, color: Sg.tx2)),
          ]),
        ),
        if (d.unread > 0)
          GestureDetector(
            onTap: vm.marking
                ? null
                : () async {
                    final error = await vm.markAllRead();
                    if (error != null && context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error)));
                  },
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 4),
              child: Text(vm.marking ? 'Marking…' : 'Mark all read', style: sgText(14, weight: FontWeight.w700, color: vm.marking ? Sg.tx3 : Sg.brand)),
            ),
          ),
      ]),
      if (d.items.isEmpty) _empty(),
      for (final entry in groups.entries) ...[
        GroupLabel(entry.key),
        SgCard(
          padding: EdgeInsets.zero,
          child: Column(children: [
            for (var i = 0; i < entry.value.length; i++) ...[
              _NotificationRow(item: entry.value[i], first: i == 0, last: i == entry.value.length - 1),
              if (i < entry.value.length - 1) const Divider(height: 1, indent: 71, color: Sg.line2),
            ],
          ]),
        ),
      ],
    ]);
  }

  static Widget _empty() => Padding(
        padding: const EdgeInsets.only(top: 60),
        child: Column(children: [
          const Icon(Icons.notifications_none_rounded, size: 34, color: Sg.tx4),
          const SizedBox(height: 10),
          Text('No notifications yet', style: sgText(15, weight: FontWeight.w600, color: Sg.tx2)),
          const SizedBox(height: 4),
          Text('You get one for each new alert in your provinces,\nfor the kinds switched on in your profile.',
              textAlign: TextAlign.center, style: sgText(13, color: Sg.tx3, height: 1.4)),
        ]),
      );
}

class _NotificationRow extends StatelessWidget {
  const _NotificationRow({required this.item, required this.first, required this.last});
  final NotificationItem item;
  final bool first;
  final bool last;

  @override
  Widget build(BuildContext context) {
    final a = item.alert;
    final tone = alertTone(a);
    final unread = !item.isRead;
    // the unread tint follows the card's rounded corners at the top and bottom of a group
    final radius = BorderRadius.vertical(top: Radius.circular(first ? 20 : 0), bottom: Radius.circular(last ? 20 : 0));
    return Material(
      color: unread ? const Color(0xFFF4FAF7) : Colors.transparent,
      borderRadius: radius,
      child: InkWell(
        borderRadius: radius,
        onTap: () => Navigator.of(context).pushNamed(AppRoutes.alert, arguments: a),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(9, 13, 15, 13),
          child: Row(children: [
            // the green dot marks an unread notification
            Container(width: 6, height: 6, decoration: BoxDecoration(color: unread ? Sg.brand : Colors.transparent, shape: BoxShape.circle)),
            const SizedBox(width: 6),
            IconTile(alertIcon(a), fg: tone.fg, bg: tone.bg),
            const SizedBox(width: 13),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(alertTitle(a), style: sgText(15, weight: unread ? FontWeight.w700 : FontWeight.w500, color: unread ? Sg.tx : Sg.tx2)),
                const SizedBox(height: 2),
                Text('${item.locationName} · ${a.sensorId}', style: sgText(13, color: Sg.tx2)),
              ]),
            ),
            Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
              Text(hhmm(item.notification.createdAt), style: sgText(12, color: Sg.tx3)),
              const SizedBox(height: 4),
              Text(a.state.stored, style: sgText(12, weight: FontWeight.w600, color: Tone.forState(a.state).fg)),
            ]),
          ]),
        ),
      ),
    );
  }
}
