// SUMMARY: Screen M2, Home: a greeting, the sensor network summary (14 of 15 reporting, one square per sensor), the newest open alert,
// and a card for each of the person's provinces. Tapping the avatar opens the profile; tapping a province opens its detail.
// MVVM: View. Everything shown comes from HomeViewModel; this file only draws it and opens other pages.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/format.dart';
import '../../core/theme.dart';
import '../../models/entities.dart';
import '../../models/enums.dart';
import '../../models/screen_models.dart';
import '../../viewmodels/home_view_model.dart';
import '../../viewmodels/view_model_factory.dart';
import '../routes.dart';
import '../widgets/alert_text.dart';
import '../widgets/widgets.dart';
import 'main_shell.dart';

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key, required this.onSeeAll});

  final VoidCallback onSeeAll;

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
        create: (c) => c.read<ViewModelFactory>().home()..load(),
        child: Consumer<HomeViewModel>(
          builder: (context, vm, _) => ViewModelBody<HomeData>(
            viewModel: vm,
            builder: (context, data) => TabPage(children: [
              Row(children: [
                Expanded(
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Text(longDate(data.nowUtc), style: sgText(13, weight: FontWeight.w600, color: Sg.tx3)),
                    const SizedBox(height: 2),
                    Text('${vm.greeting(sast(data.nowUtc).hour)}\n${vm.user.name}', style: sgText(24, weight: FontWeight.w700, spacing: -0.6, height: 1.2)),
                  ]),
                ),
                Avatar(initials(vm.user.name), onTap: () => Navigator.of(context).pushNamed(AppRoutes.profile)),
              ]),
              const SizedBox(height: 16),
              _NetworkCard(data: data),
              if (data.topAlert != null) ...[
                const SizedBox(height: 12),
                _AlertPreview(alert: data.topAlert!, names: data.locationNames),
              ],
              SectionHeader('Your provinces', action: 'See all', onAction: onSeeAll),
              for (final c in data.locations) LocationCard(card: c),
            ]),
          ),
        ),
      );
}

class _NetworkCard extends StatelessWidget {
  const _NetworkCard({required this.data});
  final HomeData data;

  static const _colours = {
    NodeState.inRange: Color(0xFF61C9A0),
    NodeState.breach: Color(0xFFC0392F),
    NodeState.warning: Color(0xFFDE9A2E),
  };

  @override
  Widget build(BuildContext context) => GlowPanel(
        padding: const EdgeInsets.fromLTRB(20, 18, 20, 18),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            Text('Sensor network', style: sgText(13, weight: FontWeight.w600, color: const Color(0xFF9FB5AC))),
            const Spacer(),
            Container(
              height: 24,
              padding: const EdgeInsets.symmetric(horizontal: 10),
              decoration: BoxDecoration(color: const Color(0x2961C9A0), borderRadius: BorderRadius.circular(12)),
              child: Row(mainAxisSize: MainAxisSize.min, children: [
                Container(width: 6, height: 6, decoration: const BoxDecoration(color: Sg.mint, shape: BoxShape.circle)),
                const SizedBox(width: 6),
                Text('Live', style: sgText(12, weight: FontWeight.w600, color: Sg.mint)),
              ]),
            ),
          ]),
          const SizedBox(height: 4),
          Row(crossAxisAlignment: CrossAxisAlignment.baseline, textBaseline: TextBaseline.alphabetic, children: [
            Text('${data.reporting}', style: sgText(44, weight: FontWeight.w700, color: Colors.white, spacing: -1.5, height: 1)),
            const SizedBox(width: 8),
            Text('of ${data.total} sensors reporting', style: sgText(13, color: const Color(0xFF9FB5AC))),
          ]),
          const SizedBox(height: 8),
          for (final row in data.grid)
            Padding(
              padding: const EdgeInsets.only(top: 9),
              child: Row(children: [
                SizedBox(width: 84, child: Text(row.locationName, style: sgText(13, color: Colors.white))),
                for (final n in row.nodes)
                  Container(
                    width: 26,
                    height: 26,
                    margin: const EdgeInsets.only(right: 8),
                    decoration: BoxDecoration(
                      color: _colours[n],
                      borderRadius: BorderRadius.circular(8),
                      border: n == NodeState.offline ? Border.all(color: const Color(0xFF9FB5AC), width: 1.5) : null,
                    ),
                  ),
              ]),
            ),
          const SizedBox(height: 13),
          Wrap(spacing: 12, runSpacing: 6, children: [
            _legend('In range', const Color(0xFF61C9A0)),
            _legend('Warning', const Color(0xFFDE9A2E)),
            _legend('Breach', const Color(0xFFC0392F)),
            _legend('Offline', null),
          ]),
          const Padding(padding: EdgeInsets.symmetric(vertical: 13), child: Divider(height: 1, color: Color(0x1AFFFFFF))),
          Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
            _stat('${data.readingsToday}', 'Readings today'),
            _stat('${data.openAlerts}', 'Open alerts'),
            _stat(hhmm(data.lastSync), 'Last sync'),
          ]),
        ]),
      );

  Widget _legend(String text, Color? c) => Row(mainAxisSize: MainAxisSize.min, children: [
        Container(
          width: 8,
          height: 8,
          decoration: BoxDecoration(color: c, borderRadius: BorderRadius.circular(3), border: c == null ? Border.all(color: const Color(0xFF9FB5AC), width: 1.5) : null),
        ),
        const SizedBox(width: 5),
        Text(text, style: sgText(11, weight: FontWeight.w500, color: const Color(0xFF9FB5AC))),
      ]);

  Widget _stat(String value, String label) => Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(value, style: sgText(16, weight: FontWeight.w700, color: Colors.white)),
        const SizedBox(height: 1),
        Text(label, style: sgText(12, color: const Color(0xFF9FB5AC))),
      ]);
}

class _AlertPreview extends StatelessWidget {
  const _AlertPreview({required this.alert, required this.names});
  final AlertItem alert;
  final Map<String, String> names;

  @override
  Widget build(BuildContext context) {
    final tone = alertTone(alert);
    return SgCard(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      onTap: () => Navigator.of(context).pushNamed(AppRoutes.alert, arguments: alert),
      child: Row(children: [
        IconTile(alertIcon(alert), fg: tone.fg, bg: tone.bg),
        const SizedBox(width: 12),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(alertTitle(alert), style: sgText(15, weight: FontWeight.w600)),
            const SizedBox(height: 2),
            Text(alertWhere(alert, names), style: sgText(13, color: Sg.tx2)),
          ]),
        ),
        Text(hhmm(alert.raisedAt), style: sgText(12, color: Sg.tx3)),
        const Icon(Icons.chevron_right_rounded, size: 18, color: Sg.tx4),
      ]),
    );
  }
}

/// A province card used on Home (and the Sites tab uses its own larger card).
class LocationCard extends StatelessWidget {
  const LocationCard({super.key, required this.card});
  final LocationCardData card;

  @override
  Widget build(BuildContext context) {
    final tone = card.optimal ? Tone.ok : Tone.warn;
    final p = [Parameter.soilMoisture, Parameter.soilTemperature, Parameter.ph];
    return SgCard(
      margin: const EdgeInsets.only(bottom: 12),
      onTap: () => Navigator.of(context).pushNamed(AppRoutes.location, arguments: card.location.id),
      child: Column(children: [
        Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(card.location.name, style: sgText(16, weight: FontWeight.w700, spacing: -0.16)),
              const SizedBox(height: 2),
              Text('${card.sensorsReporting} of ${card.sensorsTotal} reporting', style: sgText(13, color: Sg.tx3)),
            ]),
          ),
          StatusPill(card.statusLabel, tone),
        ]),
        const SizedBox(height: 14),
        Row(children: [
          for (var i = 0; i < p.length; i++) ...[
            if (i > 0) const SizedBox(width: 8),
            Expanded(child: _tile(p[i], card.averages[p[i]])),
          ],
        ]),
      ]),
    );
  }

  Widget _tile(Parameter p, double? v) {
    final s = parameterStyle(p);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(color: Sg.canvas, borderRadius: BorderRadius.circular(12)),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Icon(s.icon, size: 16, color: s.colour),
        const SizedBox(height: 6),
        Text.rich(TextSpan(children: [
          TextSpan(text: v == null ? '–' : v.toStringAsFixed(p.decimals), style: sgText(16, weight: FontWeight.w700)),
          TextSpan(text: ' ${p.unit == 'pH' ? 'pH' : p.unit}', style: sgText(12, color: Sg.tx3)),
        ])),
      ]),
    );
  }
}
