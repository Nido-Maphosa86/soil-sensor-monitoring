// SUMMARY: Screen M5, Reading trend: one parameter of one sensor over 15 minutes, 24 hours, 7 days or 30 days, with its target band,
// the lowest / average / highest values and a marker you can drag. A sensor row lets you switch between the sensors of the province.
// 15 minutes refreshes by itself. A period without readings says so (and offers a longer one) instead of an empty chart.
// There is no advice text here: only the numbers.
// MVVM: View. TrendViewModel holds the chosen range and sensor and loads the numbers; this file only draws.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/format.dart';
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
              child: RefreshIndicator(
                color: Sg.brand,
                onRefresh: vm.reload,
                child: SingleChildScrollView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(20, 6, 20, 32),
                  child: ViewModelBody<TrendData>(
                    viewModel: vm,
                    builder: (context, d) => _content(context, vm, d),
                  ),
                ),
              ),
            ),
          ),
        ),
      );

  static Widget _content(BuildContext context, TrendViewModel vm, TrendData d) {
    final p = vm.parameter;
    final now = d.now;
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
                // "Now" while the sensor reports; "Last known" with its time when it is silent
                Text(d.offline && d.lastReadingAt != null ? 'Last known · ${hhmm(d.lastReadingAt!)}' : 'Now', style: sgText(13, color: Sg.tx2)),
                Text.rich(TextSpan(children: [
                  TextSpan(
                      text: now == null ? '–' : now.toStringAsFixed(p.decimals),
                      style: sgText(34, weight: FontWeight.w700, spacing: -1, height: 1.1, color: d.offline ? Sg.offline : Sg.tx)),
                  TextSpan(text: ' ${p.unit}', style: sgText(15, weight: FontWeight.w600, color: Sg.tx3)),
                ])),
              ]),
            ),
            d.offline ? const StatusPill('Offline', Tone.off) : StatusPill(vm.range == TrendRange.minutes15 ? 'Live · every 10 s' : 'Online', Tone.ok),
          ]),
          const SizedBox(height: 14),
          d.isEmpty
              ? _noReadings(vm, d)
              : TrendChart(points: d.points, min: d.band?.minValue, max: d.band?.maxValue, unit: p.unit, decimals: p.decimals, useDays: vm.range.span >= const Duration(days: 7)),
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

  /// Shown instead of the chart when the period has no readings, with a button for the next longer period.
  static Widget _noReadings(TrendViewModel vm, TrendData d) {
    final longer = vm.longer;
    return Container(
      height: 184,
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 16),
      decoration: BoxDecoration(borderRadius: BorderRadius.circular(14), border: Border.all(color: Sg.line)),
      child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
        const Icon(Icons.show_chart_rounded, size: 26, color: Sg.tx4),
        const SizedBox(height: 6),
        Text('No readings in the last ${vm.range.label}.', textAlign: TextAlign.center, style: sgText(14, weight: FontWeight.w600, color: Sg.tx2)),
        const SizedBox(height: 2),
        Text(d.lastReadingAt == null ? '${d.sensorId} has not reported yet.' : '${d.sensorId} last reported ${ago(d.lastReadingAt!, DateTime.now().toUtc())}.',
            textAlign: TextAlign.center, style: sgText(12, color: Sg.tx3)),
        if (longer != null) ...[
          const SizedBox(height: 10),
          GestureDetector(
            onTap: () => vm.setRange(longer),
            child: Text('Show ${longer.label}', style: sgText(14, weight: FontWeight.w700, color: Sg.brand)),
          ),
        ],
      ]),
    );
  }

  static Widget _stat(String label, double? value, Parameter p, Color colour) => Expanded(
        child: SgCard(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(label, style: sgText(12, color: Sg.tx3)),
            const SizedBox(height: 3),
            Text(value == null ? '–' : '${value.toStringAsFixed(p.decimals)}${p.unit == '%' ? '%' : ''}',
                style: sgText(16, weight: FontWeight.w700, color: value == null ? Sg.tx3 : colour)),
          ]),
        ),
      );
}
