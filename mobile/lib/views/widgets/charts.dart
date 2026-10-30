// SUMMARY: The two charts of the app, drawn by hand (no chart package): the trend line with its target band, and the nutrient bars.
// MVVM: View. They get plain numbers from a ViewModel and only draw.
import 'package:flutter/material.dart';

import '../../models/screen_models.dart';
import '../../core/format.dart';
import '../../core/theme.dart';

/// A smooth line over time with a shaded target band and a marker the person can drag to read a value.
class TrendChart extends StatefulWidget {
  const TrendChart({super.key, required this.points, required this.min, required this.max, required this.unit, required this.decimals, this.useDays = false});

  final List<TrendPoint> points;

  /// The target band (shown as a green stretch with dashed edges). Both are null when there is no band.
  final double? min;
  final double? max;
  final String unit;
  final int decimals;

  /// True for 7 and 30 day charts, where the axis shows days instead of hours.
  final bool useDays;

  @override
  State<TrendChart> createState() => _TrendChartState();
}

class _TrendChartState extends State<TrendChart> {
  int? _selected;

  @override
  Widget build(BuildContext context) {
    final points = widget.points;
    if (points.length < 2) return const SizedBox(height: 184, child: Center(child: Text('Not enough readings yet')));
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onPanDown: (d) => _pick(d.localPosition.dx, context),
      onPanUpdate: (d) => _pick(d.localPosition.dx, context),
      onTapDown: (d) => _pick(d.localPosition.dx, context),
      child: SizedBox(
        height: 184,
        width: double.infinity,
        child: CustomPaint(
          painter: _TrendPainter(points, widget.min, widget.max, widget.unit, widget.decimals, widget.useDays, _selected ?? points.length - 1),
        ),
      ),
    );
  }

  void _pick(double dx, BuildContext context) {
    final box = context.findRenderObject() as RenderBox?;
    if (box == null) return;
    final w = box.size.width;
    final i = ((dx / w) * (widget.points.length - 1)).round().clamp(0, widget.points.length - 1);
    setState(() => _selected = i);
  }
}

class _TrendPainter extends CustomPainter {
  _TrendPainter(this.points, this.min, this.max, this.unit, this.decimals, this.useDays, this.selected);

  final List<TrendPoint> points;
  final double? min;
  final double? max;
  final String unit;
  final int decimals;
  final bool useDays;
  final int selected;

  @override
  void paint(Canvas canvas, Size size) {
    const top = 14.0, axis = 22.0;
    final h = size.height - top - axis;
    final w = size.width;

    var lo = points.map((p) => p.value).reduce((a, b) => a < b ? a : b);
    var hi = points.map((p) => p.value).reduce((a, b) => a > b ? a : b);
    if (min != null) lo = lo < min! ? lo : min!;
    if (max != null) hi = hi > max! ? hi : max!;
    final pad = (hi - lo) * 0.12 + 0.001;
    lo -= pad;
    hi += pad;

    double x(int i) => i * (w / (points.length - 1));
    double y(double v) => top + (hi - v) / (hi - lo) * h;

    // Target band
    if (min != null && max != null) {
      canvas.drawRRect(RRect.fromRectAndRadius(Rect.fromLTRB(0, y(max!), w, y(min!)), const Radius.circular(6)), Paint()..color = const Color(0x2161C9A0));
      final dash = Paint()
        ..color = const Color(0xB361C9A0)
        ..strokeWidth = 1;
      for (final v in [min!, max!]) {
        for (double dx = 0; dx < w; dx += 7) {
          canvas.drawLine(Offset(dx, y(v)), Offset(dx + 3, y(v)), dash);
        }
      }
      _text(canvas, '${max!.toStringAsFixed(0)}${unit == '%' ? '%' : ''}', Offset(w - 4, y(max!) - 15), const Color(0xFF10795A), align: TextAlign.right, bold: true);
      _text(canvas, '${min!.toStringAsFixed(0)}${unit == '%' ? '%' : ''}', Offset(w - 4, y(min!) + 2), const Color(0xFF10795A), align: TextAlign.right, bold: true);
    }

    // Smooth line and the soft area under it
    final path = Path()..moveTo(x(0), y(points[0].value));
    for (var i = 1; i < points.length; i++) {
      final mx = (x(i - 1) + x(i)) / 2;
      path.cubicTo(mx, y(points[i - 1].value), mx, y(points[i].value), x(i), y(points[i].value));
    }
    final area = Path.from(path)
      ..lineTo(x(points.length - 1), top + h)
      ..lineTo(0, top + h)
      ..close();
    canvas.drawPath(
      area,
      Paint()..shader = const LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Color(0x4210795A), Color(0x0010795A)]).createShader(Rect.fromLTWH(0, top, w, h)),
    );
    canvas.drawPath(path, Paint()..style = PaintingStyle.stroke..strokeWidth = 2.5..strokeCap = StrokeCap.round..color = Sg.brand);

    // Marker and its tooltip
    final sx = x(selected), sy = y(points[selected].value);
    canvas.drawLine(Offset(sx, sy), Offset(sx, top + h), Paint()..color = const Color(0x260E1F1A)..strokeWidth = 1);
    canvas.drawCircle(Offset(sx, sy), 6, Paint()..color = Colors.white);
    canvas.drawCircle(Offset(sx, sy), 6, Paint()..style = PaintingStyle.stroke..strokeWidth = 3..color = Sg.brand);
    final label = useDays ? _dayName(points[selected].time) : hhmm(points[selected].time);
    final value = '${points[selected].value.toStringAsFixed(decimals)}${unit == '%' ? '%' : ''}';
    final tipW = 76.0;
    var tx = sx - tipW / 2;
    tx = tx.clamp(0, w - tipW).toDouble();
    final ty = (sy - 46).clamp(0, h).toDouble();
    canvas.drawRRect(RRect.fromRectAndRadius(Rect.fromLTWH(tx, ty, tipW, 32), const Radius.circular(9)), Paint()..color = Sg.deep);
    _text(canvas, label, Offset(tx + tipW / 2, ty + 3), const Color(0xFF9FB5AC), size: 11, align: TextAlign.center);
    _text(canvas, value, Offset(tx + tipW / 2, ty + 16), Colors.white, size: 12, align: TextAlign.center, bold: true);

    // Time axis: first, middle, last
    for (final i in {0, (points.length - 1) ~/ 2, points.length - 1}) {
      final t = useDays ? _dayName(points[i].time) : hhmm(points[i].time);
      final align = i == 0 ? TextAlign.left : (i == points.length - 1 ? TextAlign.right : TextAlign.center);
      final dx = i == 0 ? 0.0 : (i == points.length - 1 ? w : x(i));
      _text(canvas, t, Offset(dx, size.height - 16), i == points.length - 1 ? Sg.tx : Sg.tx4, align: align, size: 11, bold: i == points.length - 1);
    }
  }

  String _dayName(DateTime utc) {
    const names = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
    final d = sast(utc);
    return useDays && points.length > 8 ? '${d.day} ${const ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'][d.month - 1]}' : names[d.weekday - 1];
  }

  void _text(Canvas canvas, String text, Offset at, Color colour, {double size = 11, TextAlign align = TextAlign.left, bool bold = false}) {
    final tp = TextPainter(
      text: TextSpan(text: text, style: sgText(size, weight: bold ? FontWeight.w600 : FontWeight.w500, color: colour)),
      textDirection: TextDirection.ltr,
    )..layout();
    final dx = switch (align) { TextAlign.right => at.dx - tp.width, TextAlign.center => at.dx - tp.width / 2, _ => at.dx };
    tp.paint(canvas, Offset(dx, at.dy));
  }

  @override
  bool shouldRepaint(covariant _TrendPainter old) => old.points != points || old.selected != selected;
}

/// Grouped bars: nitrogen, phosphorus and potassium for each location.
class NutrientBars extends StatelessWidget {
  const NutrientBars({super.key, required this.groups});

  final List<NutrientGroup> groups;

  static const colours = [Color(0xFF2F80C2), Color(0xFFDE9A2E), Color(0xFF6C55C4)];

  @override
  Widget build(BuildContext context) => SizedBox(height: 150, width: double.infinity, child: CustomPaint(painter: _BarPainter(groups)));
}

class _BarPainter extends CustomPainter {
  _BarPainter(this.groups);
  final List<NutrientGroup> groups;

  @override
  void paint(Canvas canvas, Size size) {
    const maxV = 160.0, plotH = 108.0;
    final base = plotH + 10;
    final w = size.width;
    final grid = Paint()..color = Sg.line2;

    for (final g in [40, 80, 120, 160]) {
      final y = base - g / maxV * plotH;
      canvas.drawLine(Offset(0, y), Offset(w, y), grid);
      _text(canvas, '$g', Offset(0, y - 14), Sg.tx4, 11);
    }
    canvas.drawLine(Offset(0, base), Offset(w, base), Paint()..color = const Color(0xFFE4EBE7));

    final groupW = w / groups.length;
    for (var gi = 0; gi < groups.length; gi++) {
      final g = groups[gi];
      final values = [g.nitrogen, g.phosphorus, g.potassium];
      const barW = 18.0, gap = 6.0;
      final total = barW * 3 + gap * 2;
      final startX = gi * groupW + (groupW - total) / 2;
      for (var bi = 0; bi < 3; bi++) {
        final h = values[bi] / maxV * plotH;
        final bx = startX + bi * (barW + gap);
        canvas.drawRRect(RRect.fromRectAndRadius(Rect.fromLTWH(bx, base - h, barW, h), const Radius.circular(5)), Paint()..color = NutrientBars.colours[bi]);
        _text(canvas, values[bi].round().toString(), Offset(bx + barW / 2, base - h - 15), Sg.tx2, 11, center: true, bold: true);
      }
      _text(canvas, g.locationName, Offset(gi * groupW + groupW / 2, base + 6), Sg.tx2, 11, center: true);
    }
  }

  void _text(Canvas canvas, String text, Offset at, Color colour, double size, {bool center = false, bool bold = false}) {
    final tp = TextPainter(
      text: TextSpan(text: text, style: sgText(size, weight: bold ? FontWeight.w600 : FontWeight.w500, color: colour)),
      textDirection: TextDirection.ltr,
    )..layout();
    tp.paint(canvas, Offset(center ? at.dx - tp.width / 2 : at.dx, at.dy));
  }

  @override
  bool shouldRepaint(covariant _BarPainter old) => old.groups != groups;
}
