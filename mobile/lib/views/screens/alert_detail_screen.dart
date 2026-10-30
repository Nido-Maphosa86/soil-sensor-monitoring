// SUMMARY: Screens M7 (Alert detail) and M7b (the note sheet). The detail shows the reading against its threshold as a plain comparison
// (NO treatment advice), the activity of the alert, and the Acknowledge and Resolve buttons.
// Acknowledge and Resolve BOTH need a note: the note sheet will not confirm without one. Alert state only moves forward.
// After a successful action the person returns to the alerts list, which shows the updated alert.
// MVVM: View. AlertDetailViewModel loads the alert and does Acknowledge and Resolve (checking the rules first); this file only draws.
import 'package:flutter/material.dart' hide Threshold;
import 'package:provider/provider.dart';

import '../../core/format.dart';
import '../../core/theme.dart';
import '../../models/entities.dart';
import '../../models/enums.dart';
import '../../models/screen_models.dart';
import '../../viewmodels/alert_detail_view_model.dart';
import '../../viewmodels/view_model_factory.dart';
import '../widgets/alert_text.dart';
import '../widgets/widgets.dart';

class AlertDetailScreen extends StatelessWidget {
  const AlertDetailScreen({super.key, required this.alert});

  /// The alert that was tapped (its id and location are used to load the newest version).
  final AlertItem alert;

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
        create: (c) => c.read<ViewModelFactory>().alertDetail(alert.locationId, alert.id)..load(),
        child: Consumer<AlertDetailViewModel>(
          builder: (context, vm, _) => Scaffold(
            body: ViewModelBody<AlertDetail>(viewModel: vm, builder: (context, d) => _Body(detail: d)),
          ),
        ),
      );
}

class _Body extends StatelessWidget {
  const _Body({required this.detail});
  final AlertDetail detail;

  @override
  Widget build(BuildContext context) {
    final a = detail.alert;
    final sevTone = a.type == AlertType.offline ? Tone.off : Tone.forSeverity(a.severity);
    return Stack(children: [
      SafeArea(
        child: ListView(padding: const EdgeInsets.fromLTRB(20, 6, 20, 130), children: [
          Row(children: [RoundIconButton(Icons.chevron_left_rounded, onTap: () => Navigator.of(context).pop())]),
          const SizedBox(height: 16),
          Row(children: [
            StatusPill(a.severity.stored, sevTone),
            const SizedBox(width: 8),
            StatusPill(a.state.stored, Tone.forState(a.state)),
          ]),
          const SizedBox(height: 10),
          Text(alertTitle(a), style: sgText(24, weight: FontWeight.w700, spacing: -0.6)),
          const SizedBox(height: 4),
          Text('${detail.locationName} · ${a.sensorId} · ${raisedText(a.raisedAt, detail.nowUtc)}', style: sgText(13, color: Sg.tx2)),
          const SizedBox(height: 16),
          _reading(a, detail.band),
          const SizedBox(height: 12),
          SgCard(
            padding: const EdgeInsets.fromLTRB(18, 16, 18, 16),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text('Activity', style: sgText(15, weight: FontWeight.w600)),
              const SizedBox(height: 12),
              for (var i = 0; i < detail.activity.length; i++) _ActivityRow(line: detail.activity[i], last: i == detail.activity.length - 1),
            ]),
          ),
        ]),
      ),
      if (a.state != AlertState.resolved)
        Positioned(
          left: 0,
          right: 0,
          bottom: 0,
          child: Container(
            decoration: const BoxDecoration(gradient: LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Color(0x00F3F6F4), Sg.canvas], stops: [0, 0.4])),
            padding: const EdgeInsets.fromLTRB(20, 40, 20, 0),
            child: SafeArea(
              child: Padding(
                padding: const EdgeInsets.only(bottom: 14),
                child: Row(children: [
                  if (a.state == AlertState.open) ...[
                    Expanded(child: SgButton('Acknowledge', kind: ButtonKind.ghost, onPressed: () => _openNoteSheet(context, a, resolve: false))),
                    const SizedBox(width: 12),
                  ],
                  Expanded(child: SgButton('Resolve', onPressed: () => _openNoteSheet(context, a, resolve: true))),
                ]),
              ),
            ),
          ),
        ),
    ]);
  }

  /// The reading against its threshold: a plain comparison, no advice.
  Widget _reading(AlertItem a, Threshold? band) {
    if (a.type == AlertType.offline) {
      return SgCard(
        padding: const EdgeInsets.all(18),
        child: Row(children: [
          const IconTile(Icons.wifi_off_rounded, fg: Sg.offline, bg: Sg.tOff),
          const SizedBox(width: 13),
          Expanded(child: Text('No reading has arrived from this sensor for 2 hours or more.', style: sgText(14, color: Sg.tx2, height: 1.5))),
        ]),
      );
    }
    final p = a.parameter;
    final value = a.measuredValue ?? 0, limit = a.thresholdValue ?? 0;
    final unit = p == null ? '%' : p.unit;
    final decimals = p?.decimals ?? 0;
    final below = value < limit;
    final scaleMax = [band?.maxValue ?? 0, value, limit].reduce((x, y) => x > y ? x : y) * (below ? 1.0 : 1.15);
    final tf = (limit / scaleMax).clamp(0.0, 1.0).toDouble();
    final vf = (value / scaleMax).clamp(0.0, 1.0).toDouble();
    final pct = limit == 0 ? 0 : ((value - limit).abs() / limit * 100).round();
    return SgCard(
      padding: const EdgeInsets.fromLTRB(18, 16, 18, 16),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(crossAxisAlignment: CrossAxisAlignment.end, children: [
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text('Latest reading', style: sgText(13, color: Sg.tx2)),
              Text.rich(TextSpan(children: [
                TextSpan(text: value.toStringAsFixed(decimals), style: sgText(34, weight: FontWeight.w700, color: a.state == AlertState.resolved ? Sg.tx : Sg.outOfRange, spacing: -1, height: 1.1)),
                TextSpan(text: ' $unit', style: sgText(13, color: Sg.tx3)),
              ])),
            ]),
          ),
          Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
            Text(below ? 'Minimum' : 'Maximum', style: sgText(12, color: Sg.tx3)),
            const SizedBox(height: 2),
            Text('${limit.toStringAsFixed(decimals)} $unit', style: sgText(16, weight: FontWeight.w700)),
          ]),
        ]),
        const SizedBox(height: 16),
        LayoutBuilder(builder: (context, c) {
          final w = c.maxWidth;
          return SizedBox(
            height: 20,
            child: Stack(clipBehavior: Clip.none, children: [
              Positioned(top: 5, left: 0, right: 0, child: Container(height: 10, decoration: BoxDecoration(color: const Color(0xFFE4EBE7), borderRadius: BorderRadius.circular(5)))),
              // the green stretch is the allowed side of the limit
              Positioned(
                top: 5,
                left: below ? w * tf : 0,
                width: below ? w * (1 - tf) : w * tf,
                child: Container(height: 10, decoration: BoxDecoration(color: const Color(0x7361C9A0), borderRadius: BorderRadius.circular(5))),
              ),
              // the red stretch is the reading itself
              Positioned(
                top: 5,
                left: below ? 0 : w * tf,
                width: below ? w * vf : w * (vf - tf).clamp(0.0, 1.0),
                child: Container(height: 10, decoration: BoxDecoration(color: Sg.outOfRange, borderRadius: BorderRadius.circular(5))),
              ),
              Positioned(top: 0, left: w * tf - 1, child: Container(width: 2, height: 20, decoration: BoxDecoration(color: Sg.tx, borderRadius: BorderRadius.circular(1)))),
            ]),
          );
        }),
        const SizedBox(height: 10),
        Text('$pct% ${below ? 'below the minimum' : 'above the maximum'} for ${band?.basis.toLowerCase() ?? 'this crop'}.', style: sgText(13, color: Sg.tx2)),
      ]),
    );
  }

  /// M7b: the note sheet. The confirm button refuses an empty note.
  void _openNoteSheet(BuildContext context, AlertItem a, {required bool resolve}) {
    final vm = context.read<AlertDetailViewModel>();
    vm.clearNoteError();
    // The sheet is a new page, so the same ViewModel is handed to it with ChangeNotifierProvider.value.
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => ChangeNotifierProvider.value(value: vm, child: _NoteSheet(alert: a, resolve: resolve, parentContext: context)),
    );
  }
}

class _ActivityRow extends StatelessWidget {
  const _ActivityRow({required this.line, required this.last});
  final ActivityLine line;
  final bool last;

  @override
  Widget build(BuildContext context) => IntrinsicHeight(
        child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          SizedBox(
            width: 18,
            child: Column(children: [
              Container(
                margin: const EdgeInsets.only(top: 4),
                width: 10,
                height: 10,
                decoration: BoxDecoration(color: line.important ? Sg.outOfRange : Sg.tx4, shape: BoxShape.circle, border: Border.all(color: Colors.white, width: 2)),
              ),
              if (!last) Expanded(child: Container(width: 2, color: Sg.line2)),
            ]),
          ),
          Padding(
            padding: EdgeInsets.only(bottom: last ? 0 : 14),
            child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              SizedBox(width: 48, child: Text(hhmm(line.time), style: sgText(13, weight: FontWeight.w600))),
              ConstrainedBox(constraints: const BoxConstraints(maxWidth: 230), child: Text(line.text, style: sgText(13, color: Sg.tx2, height: 1.4))),
            ]),
          ),
        ]),
      );
}

class _NoteSheet extends StatefulWidget {
  const _NoteSheet({required this.alert, required this.resolve, required this.parentContext});

  final AlertItem alert;
  final bool resolve;
  final BuildContext parentContext;

  @override
  State<_NoteSheet> createState() => _NoteSheetState();
}

class _NoteSheetState extends State<_NoteSheet> {
  // The text box belongs to the View; the ViewModel gets its text when Confirm is pressed.
  final _note = TextEditingController();

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  Future<void> _confirm(AlertDetailViewModel vm) async {
    final parent = widget.parentContext;
    final saved = await vm.act(resolve: widget.resolve, note: _note.text);
    if (!saved || !mounted) return; // the ViewModel's noteError is shown under the box
    Navigator.of(context).pop(); // the sheet
    if (parent.mounted) {
      Navigator.of(parent).pop(); // back to the alerts list, which reloads and shows the updated alert
      showToast(parent, widget.resolve ? 'Alert resolved' : 'Alert acknowledged', detail: alertTitle(widget.alert));
    }
  }

  @override
  Widget build(BuildContext context) {
    final vm = context.watch<AlertDetailViewModel>();
    final error = vm.noteError;
    final verb = widget.resolve ? 'Resolve' : 'Acknowledge';
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: Container(
        decoration: const BoxDecoration(color: Colors.white, borderRadius: BorderRadius.vertical(top: Radius.circular(28))),
        padding: const EdgeInsets.fromLTRB(20, 10, 20, 28),
        child: SafeArea(
          top: false,
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
            Center(child: Container(width: 40, height: 5, margin: const EdgeInsets.only(bottom: 16), decoration: BoxDecoration(color: Sg.line, borderRadius: BorderRadius.circular(3)))),
            Text('$verb alert', style: sgText(20, weight: FontWeight.w700, spacing: -0.4)),
            const SizedBox(height: 4),
            Text('${alertTitle(widget.alert)} · ${widget.alert.sensorId}', style: sgText(13, color: Sg.tx2)),
            const SizedBox(height: 18),
            Text('NOTE', style: sgText(10, weight: FontWeight.w600, color: Sg.tx2, spacing: 1)),
            const SizedBox(height: 8),
            Container(
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: error != null ? Sg.outOfRange : Sg.brand, width: 1.5),
                boxShadow: [BoxShadow(color: (error != null ? Sg.outOfRange : Sg.brand).withValues(alpha: 0.12), spreadRadius: 4)],
              ),
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              child: TextField(
                controller: _note,
                autofocus: true,
                maxLines: 4,
                minLines: 3,
                maxLength: vm.maxNoteLength,
                onChanged: (_) {
                  vm.clearNoteError();
                  setState(() {}); // updates the character counter
                },
                style: sgText(15, height: 1.5),
                decoration: InputDecoration(border: InputBorder.none, isDense: true, counterText: '', hintText: 'What did you find, or what did you do?', hintStyle: sgText(15, color: Sg.tx3)),
              ),
            ),
            const SizedBox(height: 8),
            Row(children: [
              Expanded(child: Text(error ?? 'A note is required to ${verb.toLowerCase()}.', style: sgText(12, color: error != null ? Sg.outOfRange : Sg.tx3))),
              Text('${_note.text.length} / ${vm.maxNoteLength}', style: sgText(12, color: Sg.tx3)),
            ]),
            const SizedBox(height: 20),
            Row(children: [
              Expanded(child: SgButton('Cancel', kind: ButtonKind.ghost, onPressed: vm.acting ? null : () => Navigator.of(context).pop())),
              const SizedBox(width: 12),
              Expanded(child: SgButton(verb, loading: vm.acting, onPressed: () => _confirm(vm))),
            ]),
          ]),
        ),
      ),
    );
  }
}
