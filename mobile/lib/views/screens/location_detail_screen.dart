// SUMMARY: Screens M4 and M4b, Province detail. A dark header with the headline soil moisture, then three tabs:
// Readings (the other parameters against their bands as a traffic light: in range, near limit, out of range),
// Trends (open a trend for any parameter) and Sensors (battery, signal, state and last communication of each sensor).
// MVVM: View. LocationDetailViewModel loads the province and remembers the open tab; this file only draws.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/format.dart';
import '../../core/theme.dart';
import '../../models/enums.dart';
import '../../models/screen_models.dart';
import '../../viewmodels/location_detail_view_model.dart';
import '../../viewmodels/view_model_factory.dart';
import '../routes.dart';
import '../widgets/widgets.dart';

class LocationDetailScreen extends StatelessWidget {
  const LocationDetailScreen({super.key, required this.locationId});
  final String locationId;

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
        create: (c) => c.read<ViewModelFactory>().locationDetail(locationId)..load(),
        child: Consumer<LocationDetailViewModel>(
          builder: (context, vm, _) => Scaffold(
            body: ViewModelBody<LocationDetail>(
              viewModel: vm,
              builder: (context, d) => ListView(padding: EdgeInsets.zero, children: [
                _Header(detail: d, tab: vm.tab, onTab: vm.selectTab, now: vm.nowUtc),
                Padding(
                  padding: const EdgeInsets.fromLTRB(20, 14, 20, 40),
                  child: switch (vm.tab) {
                    0 => _readings(context, d),
                    1 => _trends(context, d),
                    _ => _sensors(d, vm.nowUtc),
                  },
                ),
              ]),
            ),
          ),
        ),
      );

  static void _openTrend(BuildContext context, Parameter p, String locationId) =>
      Navigator.of(context).pushNamed(AppRoutes.trend, arguments: TrendArgs(locationId, p));

  /// The six other parameters as tiles (soil moisture is the headline in the header).
  static Widget _readings(BuildContext context, LocationDetail d) {
    final tiles = d.tiles.where((t) => t.parameter != Parameter.soilMoisture).toList();
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: [
        for (final t in tiles)
          SizedBox(
            width: (MediaQuery.of(context).size.width.clamp(0, 480) - 52) / 2,
            child: _ParameterTile(tile: t, onTap: () => _openTrend(context, t.parameter, d.location.id)),
          ),
      ],
    );
  }

  static Widget _trends(BuildContext context, LocationDetail d) => Column(children: [
        for (final t in d.tiles)
          SgCard(
            margin: const EdgeInsets.only(bottom: 10),
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
            onTap: () => _openTrend(context, t.parameter, d.location.id),
            child: Row(children: [
              IconTile(parameterStyle(t.parameter).icon, fg: parameterStyle(t.parameter).colour, bg: parameterStyle(t.parameter).soft, size: 40, iconSize: 19, radius: 12),
              const SizedBox(width: 12),
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(t.parameter.label, style: sgText(15, weight: FontWeight.w600)),
                  const SizedBox(height: 2),
                  Text('Target ${_num(t.min, t.parameter)} to ${_num(t.max, t.parameter)} ${t.parameter.unit}', style: sgText(12, color: Sg.tx3)),
                ]),
              ),
              Text('${_num(t.value, t.parameter)} ${t.parameter.unit}', style: sgText(14, weight: FontWeight.w600, color: Tone.forBand(t.status).fg)),
              const SizedBox(width: 4),
              const Icon(Icons.chevron_right_rounded, size: 18, color: Sg.tx4),
            ]),
          ),
      ]);

  static Widget _sensors(LocationDetail d, DateTime now) => Column(children: [
        for (final r in d.sensors) _SensorTile(row: r, now: now),
      ]);
}

String _num(double v, Parameter p) => v.toStringAsFixed(p.decimals);

class _Header extends StatelessWidget {
  const _Header({required this.detail, required this.tab, required this.onTab, required this.now});

  final LocationDetail detail;
  final int tab;
  final ValueChanged<int> onTab;
  final DateTime now;

  @override
  Widget build(BuildContext context) {
    final moisture = detail.tiles.where((t) => t.parameter == Parameter.soilMoisture).cast<ParameterTile?>().firstWhere((_) => true, orElse: () => null);
    final loc = detail.location;
    final tone = moisture == null ? Tone.off : Tone.forBand(moisture.status);
    return ClipRRect(
      borderRadius: const BorderRadius.vertical(bottom: Radius.circular(30)),
      child: Container(
        color: Sg.deep,
        child: Stack(children: [
          Positioned(
            left: -120,
            top: -170,
            child: Container(width: 340, height: 340, decoration: const BoxDecoration(shape: BoxShape.circle, gradient: RadialGradient(colors: [Color(0xB310795A), Color(0x0010795A)]))),
          ),
          SafeArea(
            bottom: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(20, 6, 20, 18),
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
                  RoundIconButton(Icons.chevron_left_rounded, dark: true, onTap: () => Navigator.of(context).pop()),
                  Container(
                    height: 28,
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    decoration: BoxDecoration(color: const Color(0x2961C9A0), borderRadius: BorderRadius.circular(14)),
                    child: Row(mainAxisSize: MainAxisSize.min, children: [
                      Container(width: 6, height: 6, decoration: const BoxDecoration(color: Sg.mint, shape: BoxShape.circle)),
                      const SizedBox(width: 6),
                      Text('Live · updated ${ago(detail.lastUpdated, now)}', style: sgText(12, weight: FontWeight.w600, color: Sg.mint)),
                    ]),
                  ),
                  const SizedBox(width: 44),
                ]),
                const SizedBox(height: 14),
                Text(loc.name, style: sgText(24, weight: FontWeight.w700, color: Colors.white, spacing: -0.6)),
                const SizedBox(height: 3),
                Text('${loc.coordinates} · ${detail.card.sensorsTotal} sensors · ${loc.crop.toLowerCase()}', style: sgText(13, color: const Color(0xFF9FB5AC))),
                if (moisture != null) ...[
                  const SizedBox(height: 18),
                  GestureDetector(
                    onTap: () => Navigator.of(context).pushNamed(AppRoutes.trend, arguments: TrendArgs(loc.id, Parameter.soilMoisture)),
                    child: Row(crossAxisAlignment: CrossAxisAlignment.end, children: [
                      Expanded(
                        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                          Row(children: [
                            const Icon(Icons.water_drop_outlined, size: 15, color: Color(0xFF9FB5AC)),
                            const SizedBox(width: 6),
                            Text('Soil moisture', style: sgText(13, color: const Color(0xFF9FB5AC))),
                          ]),
                          const SizedBox(height: 4),
                          Text.rich(TextSpan(children: [
                            TextSpan(text: moisture.value.toStringAsFixed(1), style: sgText(44, weight: FontWeight.w700, color: Colors.white, spacing: -1.5, height: 1.05)),
                            TextSpan(text: ' %', style: sgText(20, weight: FontWeight.w700, color: const Color(0xFF9FB5AC))),
                          ])),
                        ]),
                      ),
                      StatusPill(_label(moisture.status), tone, dark: moisture.status == BandStatus.inRange),
                    ]),
                  ),
                  const SizedBox(height: 14),
                  RangeBar(value: moisture.value, min: moisture.min, max: moisture.max, colour: Sg.mint, dark: true),
                  const SizedBox(height: 8),
                  Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
                    Text('${(moisture.min - (moisture.max - moisture.min)).round()}%', style: sgText(11, weight: FontWeight.w500, color: const Color(0xFF9FB5AC))),
                    Text('Target ${moisture.min.round()} to ${moisture.max.round()}%', style: sgText(11, weight: FontWeight.w500, color: const Color(0xFF9FB5AC))),
                    Text('${(moisture.max + (moisture.max - moisture.min)).round()}%', style: sgText(11, weight: FontWeight.w500, color: const Color(0xFF9FB5AC))),
                  ]),
                ],
                const SizedBox(height: 18),
                Segmented(labels: const ['Readings', 'Trends', 'Sensors'], selected: tab, onChanged: onTab, onDark: true),
              ]),
            ),
          ),
        ]),
      ),
    );
  }
}

String _label(BandStatus s) => switch (s) {
      BandStatus.inRange => 'In range',
      BandStatus.nearLimit => 'Near limit',
      BandStatus.outOfRange => 'Out of range',
    };

class _ParameterTile extends StatelessWidget {
  const _ParameterTile({required this.tile, required this.onTap});
  final ParameterTile tile;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final s = parameterStyle(tile.parameter);
    final tone = Tone.forBand(tile.status);
    return SgCard(
      padding: const EdgeInsets.fromLTRB(14, 14, 14, 16),
      onTap: onTap,
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          IconTile(s.icon, fg: s.colour, bg: s.soft, size: 36, iconSize: 17, radius: 11),
          const Spacer(),
          Container(width: 6, height: 6, decoration: BoxDecoration(color: tone.fg, shape: BoxShape.circle)),
          const SizedBox(width: 5),
          Text(_label(tile.status), style: sgText(11, weight: FontWeight.w600, color: tone.fg)),
        ]),
        const SizedBox(height: 12),
        Text(tile.parameter.label, style: sgText(13, color: Sg.tx2)),
        const SizedBox(height: 2),
        Text.rich(TextSpan(children: [
          TextSpan(text: _num(tile.value, tile.parameter), style: sgText(24, weight: FontWeight.w700, spacing: -0.5)),
          TextSpan(text: ' ${tile.parameter.unit}', style: sgText(12, color: Sg.tx3)),
        ])),
        const SizedBox(height: 12),
        RangeBar(value: tile.value, min: tile.min, max: tile.max, colour: s.colour),
      ]),
    );
  }
}

class _SensorTile extends StatelessWidget {
  const _SensorTile({required this.row, required this.now});
  final SensorRow row;
  final DateTime now;

  @override
  Widget build(BuildContext context) {
    final tone = Tone.forKey(row.stateColourKey);
    final st = row.status;
    return SgCard(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(14),
      child: Column(children: [
        Row(children: [
          Text(row.sensor.id, style: sgText(15, weight: FontWeight.w700)),
          const Spacer(),
          StatusPill(row.stateLabel, tone),
        ]),
        const SizedBox(height: 12),
        Row(children: [
          _cell('Battery', st == null ? '–' : '${st.batteryLevel}%'),
          _cell('Signal', st?.signal ?? '–'),
          _cell('Last communication', st == null ? '–' : '${dayLabel(st.lastCommunication, now) == 'Today' ? '' : '${dayLabel(st.lastCommunication, now)} '}${hhmm(st.lastCommunication)}'),
        ]),
      ]),
    );
  }

  Widget _cell(String label, String value) => Expanded(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(label, style: sgText(12, color: Sg.tx3)),
          const SizedBox(height: 3),
          Text(value, style: sgText(14, weight: FontWeight.w600)),
        ]),
      );
}
