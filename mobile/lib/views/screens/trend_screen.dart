// SUMMARY: Screen M5, Reading trend: one parameter of one sensor over 24 hours, 7 days or 30 days, with its target band,
// the lowest / average / highest values and a marker you can drag. A sensor row lets you switch between the sensors of the province.
// There is no advice text here: only the numbers.
// MVVM: View. TrendViewModel holds the chosen range and sensor and loads the numbers; this file only draws.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/theme.dart';
import '../../models/enums.dart';
import '../../models/screen_models.dart';
import '../../viewmodels/trend_view_model.dart';
import '../../viewmodels/view_model_factory.dart';
import '../routes.dart';
import '../widgets/charts.dart';
import '../widgets/widgets.dart';

class TrendScreen extends StatelessWidget {
  const TrendScreen({super.key, required this.args});

  /// The province and the parameter to show.
  final TrendArgs args;

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
        create: (c) => c.read<ViewModelFactory>().trend(args.locationId, args.parameter)..load(),
        child: Consumer<TrendViewModel>(
          builder: (context, vm, _) => Scaffold(
            body: SafeArea(
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(20, 6, 20, 32),
                child: ViewModelBody<TrendData>(
                  viewModel: vm,
                  builder: (context, d) => _content(context, vm, d),
                ),
              ),
            ),
          ),
        ),
      );

  static Widget _content(BuildContext context, TrendViewModel vm, TrendData d) {
    final p = vm.parameter;
    final change = d.changePercent;
    final up = (change ?? 0) >= 0;
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Row(children: [
        RoundIconButton(Icons.chevron_left_rounded, onTap: () => Navigator.of(context).pop()),
        const SizedBox(width: 14),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(p.label, style: sgText(20, weight: FontWeight.w700, spacing: -0.4)),
            const SizedBox(height: 1),
            Text('${d.locationName} · ${d.sensorId}', style: sgText(13, color: Sg.tx2)),
          ]),
        ),
      ]),
      const SizedBox(height: 16),
      Segmented(
        labels: [for (final r in TrendRange.values) r.label],
        selected: TrendRange.values.indexOf(vm.range),
        onChanged: (i) => vm.setRange(TrendRange.values[i]),
      ),
      const SizedBox(height: 14),
      SgCard(
        padding: const EdgeInsets.fromLTRB(20, 18, 20, 14),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(crossAxisAlignment: CrossAxisAlignment.end, children: [
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text('Now', style: sgText(13, color: Sg.tx2)),
                Text.rich(TextSpan(children: [
                  TextSpan(text: d.now.toStringAsFixed(p.decimals), style: sgText(34, weight: FontWeight.w700, spacing: -1, height: 1.1)),
                  TextSpan(text: ' ${p.unit}', style: sgText(15, weight: FontWeight.w600, color: Sg.tx3)),
                ])),
              ]),
            ),
            if (change != null)
              StatusPill('${up ? '+' : ''}${change.toStringAsFixed(1)}% on the ${vm.comparedWith} before', up ? Tone.ok : Tone.warn),
          ]),
          const SizedBox(height: 14),
          TrendChart(points: d.points, min: d.band?.minValue, max: d.band?.maxValue, unit: p.unit, decimals: p.decimals, useDays: vm.range != TrendRange.hours24),
        ]),
      ),
      const SizedBox(height: 12),
      Row(children: [
        _stat('Lowest', d.lowest, p, Sg.outOfRange),
        const SizedBox(width: 10),
        _stat('Average', d.average, p, Sg.tx),
        const SizedBox(width: 10),
        _stat('Highest', d.highest, p, Sg.inRange),
      ]),
      const SizedBox(height: 18),
      Text('Sensor', style: sgText(12, weight: FontWeight.w600, color: Sg.tx3)),
      const SizedBox(height: 8),
      SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(children: [
          for (final id in d.sensorIds) ...[
            SgChip(id, selected: id == d.sensorId, onTap: () => vm.setSensor(id)),
            const SizedBox(width: 8),
          ],
        ]),
      ),
    ]);
  }

  static Widget _stat(String label, double value, Parameter p, Color colour) => Expanded(
        child: SgCard(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(label, style: sgText(12, color: Sg.tx3)),
            const SizedBox(height: 3),
            Text('${value.toStringAsFixed(p.decimals)}${p.unit == '%' ? '%' : ''}', style: sgText(16, weight: FontWeight.w700, color: colour)),
          ]),
        ),
      );
}
