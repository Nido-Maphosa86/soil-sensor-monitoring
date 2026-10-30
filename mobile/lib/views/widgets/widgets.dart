// SUMMARY: The small building blocks every screen reuses: cards, status pills, chips, range bars, buttons, the floating tab bar,
// loading and error boxes, and ViewModelBody (which draws a screen from its ViewModel's state).
// MVVM: View. They only draw things; they hold no business logic and never call a repository.
import 'package:flutter/material.dart';

import '../../core/theme.dart';
import '../../models/enums.dart';
import '../../viewmodels/base_view_model.dart';

/// A status colour pair: text colour and soft background.
class Tone {
  const Tone(this.fg, this.bg);
  final Color fg;
  final Color bg;

  static const ok = Tone(Sg.inRange, Sg.tOk);
  static const warn = Tone(Sg.nearLimit, Sg.tWarn);
  static const bad = Tone(Sg.outOfRange, Sg.tCrit);
  static const info = Tone(Sg.info, Sg.tInfo);
  static const off = Tone(Sg.offline, Sg.tOff);

  static Tone forBand(BandStatus s) => switch (s) {
        BandStatus.inRange => ok,
        BandStatus.nearLimit => warn,
        BandStatus.outOfRange => bad,
      };

  static Tone forSeverity(AlertSeverity s) => s == AlertSeverity.critical ? bad : warn;

  static Tone forState(AlertState s) => switch (s) {
        AlertState.open => bad,
        AlertState.acknowledged => warn,
        AlertState.resolved => ok,
      };

  static Tone forKey(String key) => switch (key) {
        'ok' => ok,
        'warn' => warn,
        _ => off,
      };
}

/// The icon and colours of each measured parameter.
({IconData icon, Color colour, Color soft}) parameterStyle(Parameter p) => switch (p) {
      Parameter.soilMoisture => (icon: Icons.water_drop_outlined, colour: const Color(0xFF2F80C2), soft: const Color(0xFFE6F0F9)),
      Parameter.soilTemperature => (icon: Icons.thermostat_rounded, colour: const Color(0xFFC2612F), soft: const Color(0x1FC2612F)),
      Parameter.conductivity => (icon: Icons.bolt_rounded, colour: const Color(0xFFB87A15), soft: const Color(0xFFFBF0DC)),
      Parameter.ph => (icon: Icons.science_outlined, colour: const Color(0xFF6C55C4), soft: const Color(0x1F6C55C4)),
      Parameter.nitrogen => (icon: Icons.eco_outlined, colour: const Color(0xFF2F80C2), soft: const Color(0xFFE6F0F9)),
      Parameter.phosphorus => (icon: Icons.eco_outlined, colour: const Color(0xFFDE9A2E), soft: const Color(0x1FDE9A2E)),
      Parameter.potassium => (icon: Icons.eco_outlined, colour: const Color(0xFF10795A), soft: const Color(0xFFE3F1EB)),
    };

/// A white rounded card.
class SgCard extends StatelessWidget {
  const SgCard({super.key, required this.child, this.padding = const EdgeInsets.all(16), this.margin, this.onTap, this.radius = 20});

  final Widget child;
  final EdgeInsets padding;
  final EdgeInsets? margin;
  final VoidCallback? onTap;
  final double radius;

  @override
  Widget build(BuildContext context) {
    final box = Container(
      margin: margin,
      decoration: BoxDecoration(color: Sg.card, borderRadius: BorderRadius.circular(radius), boxShadow: Sg.shadow),
      child: Material(
        color: Colors.transparent,
        borderRadius: BorderRadius.circular(radius),
        child: InkWell(
          borderRadius: BorderRadius.circular(radius),
          onTap: onTap,
          child: Padding(padding: padding, child: child),
        ),
      ),
    );
    return box;
  }
}

/// A small rounded label with a dot, like "In range" or "Open".
class StatusPill extends StatelessWidget {
  const StatusPill(this.text, this.tone, {super.key, this.dark = false});

  final String text;
  final Tone tone;
  final bool dark;

  @override
  Widget build(BuildContext context) {
    final fg = dark ? Sg.mint : tone.fg;
    final bg = dark ? const Color(0x2961C9A0) : tone.bg;
    return Container(
      height: 24,
      padding: const EdgeInsets.symmetric(horizontal: 10),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(8)),
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        Container(width: 6, height: 6, decoration: BoxDecoration(color: fg, shape: BoxShape.circle)),
        const SizedBox(width: 6),
        Text(text, style: sgText(11, weight: FontWeight.w600, color: fg)),
      ]),
    );
  }
}

/// A rounded square holding an icon.
class IconTile extends StatelessWidget {
  const IconTile(this.icon, {super.key, required this.fg, required this.bg, this.size = 42, this.iconSize = 20, this.radius = 13});

  final IconData icon;
  final Color fg;
  final Color bg;
  final double size;
  final double iconSize;
  final double radius;

  @override
  Widget build(BuildContext context) => Container(
        width: size,
        height: size,
        decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(radius)),
        child: Icon(icon, color: fg, size: iconSize),
      );
}

/// A round button with an icon (back, more).
class RoundIconButton extends StatelessWidget {
  const RoundIconButton(this.icon, {super.key, this.onTap, this.dark = false});

  final IconData icon;
  final VoidCallback? onTap;
  final bool dark;

  @override
  Widget build(BuildContext context) => InkResponse(
        onTap: onTap,
        child: Container(
          width: 44,
          height: 44,
          decoration: BoxDecoration(
            color: dark ? const Color(0x1AFFFFFF) : Colors.white,
            shape: BoxShape.circle,
            border: Border.all(color: dark ? const Color(0x14FFFFFF) : Sg.line2),
          ),
          child: Icon(icon, size: 20, color: dark ? Colors.white : Sg.tx),
        ),
      );
}

class Avatar extends StatelessWidget {
  const Avatar(this.initials, {super.key, this.size = 44, this.radius = 14, this.fontSize = 15, this.onTap});

  final String initials;
  final double size;
  final double radius;
  final double fontSize;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => GestureDetector(
        onTap: onTap,
        child: Container(
          width: size,
          height: size,
          alignment: Alignment.center,
          decoration: BoxDecoration(color: Sg.mint, borderRadius: BorderRadius.circular(radius)),
          child: Text(initials, style: sgText(fontSize, weight: FontWeight.w600, color: Sg.deep)),
        ),
      );
}

/// A section title with an optional action on the right ("See all").
class SectionHeader extends StatelessWidget {
  const SectionHeader(this.title, {super.key, this.action, this.onAction});

  final String title;
  final String? action;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.fromLTRB(2, 24, 2, 12),
        child: Row(crossAxisAlignment: CrossAxisAlignment.baseline, textBaseline: TextBaseline.alphabetic, children: [
          Text(title, style: sgText(16, weight: FontWeight.w700, spacing: -0.16)),
          const Spacer(),
          if (action != null) GestureDetector(onTap: onAction, child: Text(action!, style: sgText(13, weight: FontWeight.w600, color: Sg.brand))),
        ]),
      );
}

/// A small grey label above a group ("Today", "Notifications").
class GroupLabel extends StatelessWidget {
  const GroupLabel(this.text, {super.key});
  final String text;

  @override
  Widget build(BuildContext context) =>
      Padding(padding: const EdgeInsets.fromLTRB(4, 20, 4, 10), child: Text(text, style: sgText(12, weight: FontWeight.w600, color: Sg.tx3, spacing: 0.2)));
}

class SgChip extends StatelessWidget {
  const SgChip(this.label, {super.key, this.count, this.selected = false, this.onTap});

  final String label;
  final int? count;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => GestureDetector(
        onTap: onTap,
        child: Container(
          height: 36,
          padding: const EdgeInsets.symmetric(horizontal: 15),
          decoration: BoxDecoration(
            color: selected ? Sg.deep : Colors.white,
            borderRadius: BorderRadius.circular(11),
            border: Border.all(color: selected ? Sg.deep : Sg.line),
          ),
          child: Row(mainAxisSize: MainAxisSize.min, children: [
            Text(label, style: sgText(13, weight: FontWeight.w600, color: selected ? Colors.white : Sg.tx2)),
            if (count != null) ...[
              const SizedBox(width: 7),
              Text('$count', style: sgText(13, weight: FontWeight.w600, color: selected ? const Color(0xFF9FB5AC) : Sg.tx4)),
            ],
          ]),
        ),
      );
}

/// A row of tabs like "Readings | Trends | Sensors" or "24 hours | 7 days | 30 days".
class Segmented extends StatelessWidget {
  const Segmented({super.key, required this.labels, required this.selected, required this.onChanged, this.onDark = false});

  final List<String> labels;
  final int selected;
  final ValueChanged<int> onChanged;
  final bool onDark;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(4),
        decoration: BoxDecoration(
          color: onDark ? Colors.white : const Color(0xFFE4EBE7),
          borderRadius: BorderRadius.circular(14),
          boxShadow: onDark ? Sg.shadow : null,
        ),
        child: Row(children: [
          for (var i = 0; i < labels.length; i++)
            Expanded(
              child: GestureDetector(
                behavior: HitTestBehavior.opaque,
                onTap: () => onChanged(i),
                child: Container(
                  height: 38,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: i == selected ? (onDark ? Sg.tOk : Colors.white) : Colors.transparent,
                    borderRadius: BorderRadius.circular(11),
                    boxShadow: i == selected && !onDark ? Sg.shadow : null,
                  ),
                  child: Text(labels[i],
                      style: sgText(13, weight: FontWeight.w600, color: i == selected ? (onDark ? Sg.brand : Sg.tx) : Sg.tx2)),
                ),
              ),
            ),
        ]),
      );
}

enum ButtonKind { primary, ghost, dark }

class SgButton extends StatelessWidget {
  const SgButton(this.label, {super.key, required this.onPressed, this.kind = ButtonKind.primary, this.icon, this.loading = false});

  final String label;
  final VoidCallback? onPressed;
  final ButtonKind kind;
  final IconData? icon;
  final bool loading;

  @override
  Widget build(BuildContext context) {
    final fg = kind == ButtonKind.ghost ? Sg.tx : Colors.white;
    final bg = switch (kind) { ButtonKind.primary => Sg.brand, ButtonKind.ghost => Colors.white, ButtonKind.dark => Sg.deep };
    return Opacity(
      opacity: onPressed == null && !loading ? 0.5 : 1,
      child: GestureDetector(
        onTap: loading ? null : onPressed,
        child: Container(
          height: 54,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: bg,
            borderRadius: BorderRadius.circular(14),
            border: kind == ButtonKind.ghost ? Border.all(color: Sg.line) : null,
            boxShadow: kind == ButtonKind.primary ? Sg.shadowButton : null,
          ),
          child: loading
              ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.4, color: Colors.white))
              : Row(mainAxisSize: MainAxisSize.min, children: [
                  if (icon != null) ...[Icon(icon, size: 19, color: fg), const SizedBox(width: 8)],
                  Text(label, style: sgText(15, weight: FontWeight.w600, color: fg)),
                ]),
        ),
      ),
    );
  }
}

/// A thin horizontal bar showing a share (for example 88% of readings in range).
class ShareBar extends StatelessWidget {
  const ShareBar(this.fraction, this.colour, {super.key, this.height = 6});

  final double fraction;
  final Color colour;
  final double height;

  @override
  Widget build(BuildContext context) => ClipRRect(
        borderRadius: BorderRadius.circular(height / 2),
        child: Container(
          height: height,
          color: const Color(0xFFE4EBE7),
          alignment: Alignment.centerLeft,
          child: FractionallySizedBox(widthFactor: fraction.clamp(0, 1).toDouble(), child: Container(color: colour)),
        ),
      );
}

/// Shows where a value sits inside its target band (the green stretch), with a round marker.
class RangeBar extends StatelessWidget {
  const RangeBar({super.key, required this.value, required this.min, required this.max, required this.colour, this.dark = false});

  final double value;
  final double min;
  final double max;
  final Color colour;
  final bool dark;

  @override
  Widget build(BuildContext context) {
    final span = max - min;
    final lo = min - span / 2, hi = max + span / 2;
    final p = ((value - lo) / (hi - lo)).clamp(0.03, 0.97).toDouble();
    return SizedBox(
      height: 14,
      child: LayoutBuilder(builder: (context, c) {
        final w = c.maxWidth;
        return Stack(clipBehavior: Clip.none, alignment: Alignment.centerLeft, children: [
          Container(height: dark ? 8 : 6, decoration: BoxDecoration(color: dark ? const Color(0x1FFFFFFF) : const Color(0xFFE4EBE7), borderRadius: BorderRadius.circular(4))),
          Positioned(
            left: w * 0.25,
            width: w * 0.5,
            child: Container(height: dark ? 8 : 6, decoration: BoxDecoration(color: const Color(0x7361C9A0), borderRadius: BorderRadius.circular(4))),
          ),
          Positioned(
            left: w * p - 6,
            child: Container(
              width: 12,
              height: 12,
              decoration: BoxDecoration(color: Colors.white, shape: BoxShape.circle, border: Border.all(color: colour, width: 3)),
            ),
          ),
        ]);
      }),
    );
  }
}

/// A dark green panel with a soft glow in the corner (used for the home network card and location headers).
class GlowPanel extends StatelessWidget {
  const GlowPanel({super.key, required this.child, this.radius = 26, this.padding = const EdgeInsets.all(20)});

  final Widget child;
  final double radius;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) => ClipRRect(
        borderRadius: BorderRadius.circular(radius),
        child: Container(
          color: Sg.deep,
          child: Stack(children: [
            Positioned(
              right: -120,
              top: -150,
              child: Container(
                width: 300,
                height: 300,
                decoration: const BoxDecoration(shape: BoxShape.circle, gradient: RadialGradient(colors: [Color(0x9910795A), Color(0x0010795A)])),
              ),
            ),
            Padding(padding: padding, child: child),
          ]),
        ),
      );
}

/// The floating bottom tab bar: Home, Sites, Alerts, Reports. Alerts carries the open count.
class FloatingTabBar extends StatelessWidget {
  const FloatingTabBar({super.key, required this.index, required this.onTap, required this.openAlerts});

  final int index;
  final ValueChanged<int> onTap;
  final int openAlerts;

  static const _tabs = [
    (Icons.home_outlined, Icons.home_rounded, 'Home'),
    (Icons.place_outlined, Icons.place_rounded, 'Sites'),
    (Icons.notifications_none_rounded, Icons.notifications_rounded, 'Alerts'),
    (Icons.bar_chart_rounded, Icons.bar_chart_rounded, 'Reports'),
  ];

  @override
  Widget build(BuildContext context) => Container(
        height: 68,
        padding: const EdgeInsets.symmetric(horizontal: 6),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(24),
          boxShadow: Sg.shadowFloat,
          border: Border.all(color: Sg.line2),
        ),
        child: Row(children: [
          for (var i = 0; i < _tabs.length; i++)
            Expanded(
              child: GestureDetector(
                behavior: HitTestBehavior.opaque,
                onTap: () => onTap(i),
                child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
                  Stack(clipBehavior: Clip.none, children: [
                    Container(
                      width: 54,
                      height: 30,
                      decoration: BoxDecoration(color: i == index ? Sg.tOk : Colors.transparent, borderRadius: BorderRadius.circular(15)),
                      child: Icon(i == index ? _tabs[i].$2 : _tabs[i].$1, size: 22, color: i == index ? Sg.brand : Sg.tx3),
                    ),
                    if (i == 2 && openAlerts > 0)
                      Positioned(
                        top: -3,
                        right: 7,
                        child: Container(
                          constraints: const BoxConstraints(minWidth: 16),
                          height: 16,
                          padding: const EdgeInsets.symmetric(horizontal: 4),
                          alignment: Alignment.center,
                          decoration: BoxDecoration(color: Sg.outOfRange, borderRadius: BorderRadius.circular(8), border: Border.all(color: Colors.white, width: 2)),
                          child: Text('$openAlerts', style: sgText(10, weight: FontWeight.w600, color: Colors.white)),
                        ),
                      ),
                  ]),
                  const SizedBox(height: 3),
                  Text(_tabs[i].$3, style: sgText(11, weight: i == index ? FontWeight.w600 : FontWeight.w500, color: i == index ? Sg.brand : Sg.tx3)),
                ]),
              ),
            ),
        ]),
      );
}

class LoadingBox extends StatelessWidget {
  const LoadingBox({super.key});

  @override
  Widget build(BuildContext context) => const Padding(
        padding: EdgeInsets.all(48),
        child: Center(child: SizedBox(width: 28, height: 28, child: CircularProgressIndicator(strokeWidth: 3, color: Sg.brand))),
      );
}

/// Shown when loading fails (including "waiting for the Firebase connection"). A retry button reloads.
class ErrorBox extends StatelessWidget {
  const ErrorBox(this.error, {super.key, required this.onRetry});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.all(24),
        child: SgCard(
          padding: const EdgeInsets.all(20),
          child: Column(children: [
            const IconTile(Icons.cloud_off_rounded, fg: Sg.nearLimit, bg: Sg.tWarn),
            const SizedBox(height: 14),
            Text('Nothing to show yet', style: sgText(16, weight: FontWeight.w700)),
            const SizedBox(height: 6),
            Text(error.toString(), textAlign: TextAlign.center, style: sgText(13, color: Sg.tx2, height: 1.5)),
            const SizedBox(height: 16),
            SgButton('Try again', onPressed: onRetry, kind: ButtonKind.ghost),
          ]),
        ),
      );
}

/// Draws a screen from its ViewModel: a spinner while loading, an error box with Retry when loading failed,
/// and the real content (from [builder]) when the data is ready. Every screen uses this, so they all behave the same.
class ViewModelBody<T> extends StatelessWidget {
  const ViewModelBody({super.key, required this.viewModel, required this.builder});

  final LoadingViewModel<T> viewModel;
  final Widget Function(BuildContext context, T data) builder;

  @override
  Widget build(BuildContext context) => switch (viewModel.state) {
        LoadState.loading => const LoadingBox(),
        LoadState.failed => ErrorBox(viewModel.error!, onRetry: viewModel.load),
        LoadState.ready => builder(context, viewModel.data),
      };
}

/// A bottom-floating message like "Report saved".
void showToast(BuildContext context, String title, {String? detail, bool error = false}) {
  final messenger = ScaffoldMessenger.of(context);
  messenger.hideCurrentSnackBar();
  messenger.showSnackBar(SnackBar(
    behavior: SnackBarBehavior.floating,
    backgroundColor: Colors.white,
    elevation: 6,
    margin: const EdgeInsets.fromLTRB(20, 0, 20, 108),
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16), side: const BorderSide(color: Sg.line2)),
    content: Row(children: [
      IconTile(error ? Icons.error_outline_rounded : Icons.check_circle_outline_rounded, fg: error ? Sg.outOfRange : Sg.brand, bg: error ? Sg.tCrit : Sg.tOk, size: 36, iconSize: 18, radius: 11),
      const SizedBox(width: 12),
      Expanded(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisSize: MainAxisSize.min, children: [
          Text(title, style: sgText(15, weight: FontWeight.w600)),
          if (detail != null) Text(detail, maxLines: 2, overflow: TextOverflow.ellipsis, style: sgText(12, color: Sg.tx2)),
        ]),
      ),
    ]),
  ));
}
