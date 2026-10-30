// SUMMARY: Screen M6, Alerts: counters (open, acknowledged, resolved), the filters All / Critical / Warning / Offline / Acknowledged / Resolved,
// and the alerts grouped by day. Tapping an alert opens its detail (M7). The open count also shows as a badge on the Alerts tab.
// MVVM: View. The counters, the filter and the grouped alerts come from AlertsViewModel; this file only draws them.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/format.dart';
import '../../core/theme.dart';
import '../../models/entities.dart';
import '../../models/screen_models.dart';
import '../../viewmodels/alerts_view_model.dart';
import '../../viewmodels/view_model_factory.dart';
import '../routes.dart';
import '../widgets/alert_text.dart';
import '../widgets/widgets.dart';
import 'main_shell.dart';

class AlertsScreen extends StatelessWidget {
  const AlertsScreen({super.key});

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
        create: (c) => c.read<ViewModelFactory>().alerts()..load(),
        child: Consumer<AlertsViewModel>(
          builder: (context, vm, _) => ViewModelBody<AlertsOverview>(
            viewModel: vm,
            builder: (context, o) {
              final groups = vm.groups;
              return TabPage(children: [
                const SizedBox(height: 10),
                Text('Alerts', style: sgText(28, weight: FontWeight.w700, spacing: -0.7, height: 1.15)),
                const SizedBox(height: 4),
                Text('${o.open} open · ${o.acknowledged} acknowledged', style: sgText(13, color: Sg.tx2)),
                const SizedBox(height: 16),
                Row(children: [
                  _counter('${o.open}', 'Open', Sg.outOfRange),
                  const SizedBox(width: 10),
                  _counter('${o.acknowledged}', 'Acknowledged', Sg.nearLimit),
                  const SizedBox(width: 10),
                  _counter('${o.resolved}', 'Resolved', Sg.inRange),
                ]),
                const SizedBox(height: 14),
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(children: [
                    for (final f in AlertFilter.values) ...[
                      SgChip(f.label, selected: f == vm.filter, onTap: () => vm.setFilter(f)),
                      const SizedBox(width: 8),
                    ],
                  ]),
                ),
                for (final entry in groups.entries) ...[
                  GroupLabel(entry.key),
                  SgCard(
                    padding: EdgeInsets.zero,
                    child: Column(children: [
                      for (var i = 0; i < entry.value.length; i++) ...[
                        _AlertRow(alert: entry.value[i], names: o.locationNames),
                        if (i < entry.value.length - 1) const Divider(height: 1, indent: 71, color: Sg.line2),
                      ],
                    ]),
                  ),
                ],
                if (o.alerts.isEmpty)
                  Padding(padding: const EdgeInsets.all(32), child: Center(child: Text('No alerts match this filter.', style: sgText(13, color: Sg.tx3)))),
              ]);
            },
          ),
        ),
      );

  static Widget _counter(String n, String label, Color colour) => Expanded(
        child: SgCard(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 13),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(n, style: sgText(24, weight: FontWeight.w700, color: colour, spacing: -0.5)),
            const SizedBox(height: 2),
            FittedBox(fit: BoxFit.scaleDown, alignment: Alignment.centerLeft, child: Text(label, maxLines: 1, style: sgText(12, color: Sg.tx2))),
          ]),
        ),
      );
}

class _AlertRow extends StatelessWidget {
  const _AlertRow({required this.alert, required this.names});
  final AlertItem alert;
  final Map<String, String> names;

  @override
  Widget build(BuildContext context) {
    final tone = alertTone(alert);
    return InkWell(
      borderRadius: BorderRadius.circular(20),
      onTap: () => Navigator.of(context).pushNamed(AppRoutes.alert, arguments: alert),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 15, vertical: 13),
        child: Row(children: [
          IconTile(alertIcon(alert), fg: tone.fg, bg: tone.bg),
          const SizedBox(width: 13),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(alertTitle(alert), style: sgText(15, weight: FontWeight.w600)),
              const SizedBox(height: 2),
              Text(alertWhere(alert, names), style: sgText(13, color: Sg.tx2)),
            ]),
          ),
          Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
            Text(hhmm(alert.raisedAt), style: sgText(12, color: Sg.tx3)),
            const SizedBox(height: 4),
            Text(alert.state.stored, style: sgText(12, weight: FontWeight.w600, color: Tone.forState(alert.state).fg)),
          ]),
        ]),
      ),
    );
  }
}
