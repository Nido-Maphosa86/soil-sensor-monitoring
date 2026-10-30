// SUMMARY: Screen M3, Sites: the person's assigned provinces, each with the share of readings in range.
// A search box and the filters All / Optimal / Attention narrow the list. Tapping a card opens the province detail (M4).
// MVVM: View. The provinces, the search text and the filter live in SitesViewModel; this file only draws them.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/theme.dart';
import '../../models/screen_models.dart';
import '../../viewmodels/sites_view_model.dart';
import '../../viewmodels/view_model_factory.dart';
import '../routes.dart';
import '../widgets/widgets.dart';
import 'main_shell.dart';

class SitesScreen extends StatelessWidget {
  const SitesScreen({super.key});

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
        create: (c) => c.read<ViewModelFactory>().sites()..load(),
        child: Consumer<SitesViewModel>(
          builder: (context, vm, _) => ViewModelBody<List<LocationCardData>>(
            viewModel: vm,
            builder: (context, all) {
              final shown = vm.shown;
              return TabPage(children: [
                const SizedBox(height: 10),
                Text('Sites', style: sgText(28, weight: FontWeight.w700, spacing: -0.7, height: 1.15)),
                const SizedBox(height: 4),
                Text('${all.length} provinces · ${vm.sensorTotal} sensors · ${vm.crops}', style: sgText(13, color: Sg.tx2)),
                const SizedBox(height: 16),
                Container(
                  height: 48,
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                  decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(14), border: Border.all(color: Sg.line)),
                  child: Row(children: [
                    const Icon(Icons.search_rounded, size: 18, color: Sg.tx3),
                    const SizedBox(width: 10),
                    Expanded(
                      child: TextField(
                        onChanged: vm.setQuery,
                        style: sgText(13),
                        decoration: InputDecoration(border: InputBorder.none, isDense: true, hintText: 'Search a province', hintStyle: sgText(13, color: Sg.tx3)),
                      ),
                    ),
                  ]),
                ),
                const SizedBox(height: 12),
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(children: [
                    SgChip('All', count: all.length, selected: vm.filter == SiteFilter.all, onTap: () => vm.setFilter(SiteFilter.all)),
                    const SizedBox(width: 8),
                    SgChip('Optimal', count: vm.optimalCount, selected: vm.filter == SiteFilter.optimal, onTap: () => vm.setFilter(SiteFilter.optimal)),
                    const SizedBox(width: 8),
                    SgChip('Attention', count: vm.attentionCount, selected: vm.filter == SiteFilter.attention, onTap: () => vm.setFilter(SiteFilter.attention)),
                  ]),
                ),
                const SizedBox(height: 14),
                for (final c in shown) _SiteCard(card: c),
                if (shown.isEmpty) Padding(padding: const EdgeInsets.all(24), child: Center(child: Text('No provinces match.', style: sgText(13, color: Sg.tx3)))),
              ]);
            },
          ),
        ),
      );
}

class _SiteCard extends StatelessWidget {
  const _SiteCard({required this.card});
  final LocationCardData card;

  @override
  Widget build(BuildContext context) {
    final tone = card.optimal ? Tone.ok : Tone.warn;
    final loc = card.location;
    return SgCard(
      padding: const EdgeInsets.all(18),
      margin: const EdgeInsets.only(bottom: 12),
      onTap: () => Navigator.of(context).pushNamed(AppRoutes.location, arguments: loc.id),
      child: Column(children: [
        Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Container(width: 4, height: 40, decoration: BoxDecoration(color: Sg.locationColour(loc.id), borderRadius: BorderRadius.circular(2))),
          const SizedBox(width: 12),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(loc.name, style: sgText(16, weight: FontWeight.w700, spacing: -0.24)),
              const SizedBox(height: 3),
              Row(children: [
                const Icon(Icons.place_outlined, size: 13, color: Sg.tx3),
                const SizedBox(width: 5),
                Text(loc.coordinates, style: sgText(12, color: Sg.tx3)),
              ]),
            ]),
          ),
          StatusPill(card.statusLabel, tone),
        ]),
        const SizedBox(height: 16),
        Row(children: [
          Text('Readings in range', style: sgText(13, color: Sg.tx2)),
          const Spacer(),
          Text('${card.inRangePercent}%', style: sgText(15, weight: FontWeight.w600, color: tone.fg)),
        ]),
        const SizedBox(height: 8),
        ShareBar(card.inRangePercent / 100, tone.fg),
        const Padding(padding: EdgeInsets.only(top: 14, bottom: 12), child: Divider(height: 1, color: Sg.line2)),
        Row(children: [
          _stat('Sensors', '${card.sensorsReporting} of ${card.sensorsTotal}'),
          _stat('Readings today', '${card.readingsToday}'),
          _stat('Crop', loc.crop),
        ]),
      ]),
    );
  }

  Widget _stat(String label, String value) => Expanded(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(label, style: sgText(12, color: Sg.tx3)),
          const SizedBox(height: 3),
          Text(value, style: sgText(15, weight: FontWeight.w600)),
        ]),
      );
}
