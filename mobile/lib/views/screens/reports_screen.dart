// SUMMARY: Screen M8, Reports: the nutrient levels of the person's provinces and the share of readings in range over the last 24 hours,
// worked out from the readings. Report FILES are made and downloaded on the web console, so the tab says where to find them.
// MVVM: View. ReportsViewModel loads the numbers (checking rule 4); this file only draws.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/theme.dart';
import '../../models/screen_models.dart';
import '../../viewmodels/reports_view_model.dart';
import '../../viewmodels/view_model_factory.dart';
import '../widgets/charts.dart';
import '../widgets/widgets.dart';
import 'main_shell.dart';

class ReportsScreen extends StatelessWidget {
  const ReportsScreen({super.key});

  static Color _colour(int? percent) => percent == null ? Sg.offline : percent >= 70 ? Sg.inRange : Sg.nearLimit;

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
        create: (c) => c.read<ViewModelFactory>().reports()..load(),
        child: Consumer<ReportsViewModel>(
          builder: (context, vm, _) => ViewModelBody<ReportsData>(
            viewModel: vm,
            builder: (context, d) {
        return TabPage(onRefresh: vm.reload, children: [
          const SizedBox(height: 10),
          Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text('Reports', style: sgText(28, weight: FontWeight.w700, spacing: -0.7, height: 1.15)),
                const SizedBox(height: 4),
                Text('Nutrients across your provinces', style: sgText(13, color: Sg.tx2)),
              ]),
            ),
            Container(
              height: 36,
              margin: const EdgeInsets.only(top: 4),
              padding: const EdgeInsets.symmetric(horizontal: 12),
              decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(11), border: Border.all(color: Sg.line)),
              child: Row(mainAxisSize: MainAxisSize.min, children: [
                const Icon(Icons.schedule_rounded, size: 15, color: Sg.tx),
                const SizedBox(width: 6),
                Text(d.periodLabel, style: sgText(13, weight: FontWeight.w600)),
              ]),
            ),
          ]),
          const SizedBox(height: 16),
          SgCard(
            padding: const EdgeInsets.fromLTRB(20, 16, 20, 12),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text('Nutrient levels by province', style: sgText(16, weight: FontWeight.w700)),
              const SizedBox(height: 1),
              Text('Average of the last 24 hours (one reading per hour), mg/kg', style: sgText(12, color: Sg.tx3)),
              const SizedBox(height: 10),
              Row(children: [
                for (final (i, n) in ['Nitrogen', 'Phosphorus', 'Potassium'].indexed) ...[
                  Container(width: 8, height: 8, decoration: BoxDecoration(color: NutrientBars.colours[i], borderRadius: BorderRadius.circular(3))),
                  const SizedBox(width: 5),
                  Text(n, style: sgText(11, weight: FontWeight.w600, color: Sg.tx2)),
                  const SizedBox(width: 12),
                ],
              ]),
              const SizedBox(height: 8),
              NutrientBars(groups: d.nutrients),
            ]),
          ),
          const SizedBox(height: 10),
          SgCard(
            padding: const EdgeInsets.fromLTRB(20, 14, 20, 14),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text('Readings in range', style: sgText(16, weight: FontWeight.w700)),
              for (final r in d.inRange) ...[
                const SizedBox(height: 9),
                Row(children: [
                  Text(r.locationName, style: sgText(13)),
                  const Spacer(),
                  Text(r.percent == null ? 'No readings' : '${r.percent}%', style: sgText(15, weight: FontWeight.w600, color: _colour(r.percent))),
                ]),
                const SizedBox(height: 6),
                ShareBar((r.percent ?? 0) / 100, _colour(r.percent)),
              ],
              const Padding(padding: EdgeInsets.only(top: 11, bottom: 9), child: Divider(height: 1, color: Sg.line2)),
              Row(children: [
                Text('All sites', style: sgText(15, weight: FontWeight.w600)),
                const Spacer(),
                Text(d.allSites == null ? '–' : '${d.allSites}%', style: sgText(15, weight: FontWeight.w600)),
              ]),
            ]),
          ),
          const GroupLabel('Report files'),
          SgCard(
            child: Row(children: [
              const IconTile(Icons.desktop_windows_outlined, fg: Sg.brand, bg: Sg.tOk, size: 38, iconSize: 19, radius: 12),
              const SizedBox(width: 13),
              Expanded(
                child: Text('The monthly summary, breach log and raw readings are made and downloaded on the SensorGrid web console.',
                    style: sgText(13, color: Sg.tx2, height: 1.45)),
              ),
            ]),
          ),
        ]);
            },
          ),
        ),
      );
}
