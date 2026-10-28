// SUMMARY: Small helpers that turn numbers and times into the text shown on screen.
// All times are stored in UTC and shown in South African time (UTC+2), as agreed.
import 'package:intl/intl.dart';

import 'app_config.dart';

/// Converts a stored UTC time to South African time for display.
DateTime sast(DateTime utc) => utc.toUtc().add(AppConfig.southAfricaOffset);

String hhmm(DateTime utc) => DateFormat('HH:mm').format(sast(utc));

/// "Today", "Yesterday" or "5 September" (relative to [nowUtc]).
String dayLabel(DateTime utc, DateTime nowUtc) {
  final d = sast(utc), n = sast(nowUtc);
  final a = DateTime(d.year, d.month, d.day), b = DateTime(n.year, n.month, n.day);
  final diff = b.difference(a).inDays;
  if (diff == 0) return 'Today';
  if (diff == 1) return 'Yesterday';
  return DateFormat('d MMMM').format(d);
}

String longDate(DateTime utc) => DateFormat('EEEE, d MMMM').format(sast(utc));

String monthYear(DateTime utc) => DateFormat('MMM yyyy').format(sast(utc));

/// "Raised today at 06:30" style text.
String raisedText(DateTime utc, DateTime nowUtc) {
  final day = dayLabel(utc, nowUtc);
  return 'raised ${day == 'Today' ? 'today' : day.toLowerCase() == 'yesterday' ? 'yesterday' : 'on $day'} at ${hhmm(utc)}';
}

/// "2 min ago", "3 h ago" and so on.
String ago(DateTime utc, DateTime nowUtc) {
  final m = nowUtc.difference(utc).inMinutes;
  if (m < 1) return 'just now';
  if (m < 60) return '$m min ago';
  final h = m ~/ 60;
  if (h < 48) return '$h h ago';
  return '${h ~/ 24} days ago';
}

/// A value with the right number of decimals: whole numbers for mg/kg and µS/cm, one decimal otherwise.
String num1(double v, {int decimals = 1}) => v.toStringAsFixed(decimals);

String fileSize(int bytes) =>
    bytes >= 1024 * 1024 ? '${(bytes / 1024 / 1024).toStringAsFixed(1)} MB' : '${(bytes / 1024).round()} KB';

String initials(String name) {
  final parts = name.split(RegExp(r'[ .]+')).where((p) => p.isNotEmpty).toList();
  if (parts.isEmpty) return '?';
  if (parts.length == 1) return parts.first[0].toUpperCase();
  return (parts.first[0] + parts.last[0]).toUpperCase();
}
