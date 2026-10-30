// SUMMARY: Screen M8, Reports: the nutrient levels of the person's provinces and the share of readings in range, worked out from the readings.
// Report FILES are generated and downloaded on the web console (the database rules keep the reports list off the phone), so the list says so.
// MVVM: View. ReportsViewModel loads the numbers and does the download (checking rule 4); this file only draws.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/format.dart';
import '../../core/theme.dart';
import '../../models/entities.dart';
import '../../models/enums.dart';
import '../../models/screen_models.dart';
import '../../viewmodels/reports_view_model.dart';
import '../../viewmodels/view_model_factory.dart';
import '../widgets/charts.dart';
import '../widgets/widgets.dart';
import 'main_shell.dart';

class ReportsScreen extends StatelessWidget {
  const ReportsScreen({super.key});

  /// Asks the ViewModel to download, then shows how it went.
  static Future<void> _download(BuildContext context, ReportsViewModel vm, Report r) async {
    final result = await vm.download(r);
    if (context.mounted) showToast(context, result.title, detail: result.detail, error: !result.ok);
  }

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
        create: (c) => c.read<ViewModelFactory>().reports()..load(),
        child: Consumer<ReportsViewModel>(
          builder: (context, vm, _) => ViewModelBody<ReportsData>(
            viewModel: vm,
            builder: (context, d) {
              final latest = vm.latest;
        return TabPage(children: [
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
                const Icon(Icons.calendar_today_outlined, size: 15, color: Sg.tx),
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
              Text('Average of the last 30 days, mg/kg', style: sgText(12, color: Sg.tx3)),
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
                  Text('${r.percent}%', style: sgText(15, weight: FontWeight.w600, color: r.percent >= 70 ? Sg.inRange : Sg.nearLimit)),
                ]),
                const SizedBox(height: 6),
                ShareBar(r.percent / 100, r.percent >= 70 ? Sg.inRange : Sg.nearLimit),
              ],
              const Padding(padding: EdgeInsets.only(top: 11, bottom: 9), child: Divider(height: 1, color: Sg.line2)),
              Row(children: [
                Text('All sites', style: sgText(15, weight: FontWeight.w600)),
                const Spacer(),
                Text('${d.allSites}%', style: sgText(15, weight: FontWeight.w600)),
              ]),
            ]),
          ),
          const SizedBox(height: 10),
          if (latest != null)
            SgButton('Download report (${latest.format.stored})', kind: ButtonKind.dark, icon: Icons.download_rounded, loading: vm.downloadingId == latest.id, onPressed: () => _download(context, vm, latest)),
          const GroupLabel('Generated reports'),
          if (d.reports.isEmpty)
            SgCard(child: Text('Report files are generated and downloaded on the SensorGrid web console.', style: sgText(13, color: Sg.tx3)))
          else
            SgCard(
              padding: EdgeInsets.zero,
              child: Column(children: [
                for (var i = 0; i < d.reports.length; i++) ...[
                  ListTile(
                    contentPadding: const EdgeInsets.symmetric(horizontal: 15, vertical: 4),
                    leading: IconTile(d.reports[i].format == ReportFormat.pdf ? Icons.picture_as_pdf_outlined : Icons.table_chart_outlined, fg: Sg.brand, bg: Sg.tOk, size: 38, iconSize: 19, radius: 12),
                    title: Text(d.reports[i].name, style: sgText(14, weight: FontWeight.w600)),
                    subtitle: Text('${d.reports[i].format.stored} · ${fileSize(d.reports[i].fileSize)} · ${dayLabel(d.reports[i].generatedAt, vm.nowUtc)}', style: sgText(12, color: Sg.tx3)),
                    trailing: vm.downloadingId == d.reports[i].id
                        ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2.4, color: Sg.brand))
                        : const Icon(Icons.download_rounded, color: Sg.brand, size: 22),
                    onTap: () => _download(context, vm, d.reports[i]),
                  ),
                  if (i < d.reports.length - 1) const Divider(height: 1, indent: 69, color: Sg.line2),
                ],
              ]),
            ),
        ]);
            },
          ),
        ),
      );
}
