// SUMMARY: The colours, text style and shapes of the whole app, taken from the SensorGrid web app and the mobile design.
// Every screen reads its colours from here, so changing a colour here changes it everywhere.
import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// The colour palette (same values as the web app).
class Sg {
  // Brand
  static const deep = Color(0xFF0C2E24); // deep green of the SensorGrid design
  static const brand = Color(0xFF10795A);
  static const brandDark = Color(0xFF0A5942);
  static const mint = Color(0xFF61C9A0);

  // Surfaces and text
  static const canvas = Color(0xFFF3F6F4);
  static const card = Color(0xFFFFFFFF);
  static const tx = Color(0xFF0E1F1A);
  static const tx2 = Color(0xFF5C6E67);
  static const tx3 = Color(0xFF8A9691);
  static const tx4 = Color(0xFFA9B8B1);
  static const line = Color(0xFFDDE5E1);
  static const line2 = Color(0xFFEEF3F0);

  // Traffic light (agreed in the design)
  static const inRange = Color(0xFF12845A);
  static const nearLimit = Color(0xFFA76A0C);
  static const outOfRange = Color(0xFFB8342A);
  static const offline = Color(0xFF8A9691);
  static const info = Color(0xFF2F80C2);

  // Soft backgrounds that go with the status colours
  static const tOk = Color(0xFFE3F1EB);
  static const tWarn = Color(0xFFFBF0DC);
  static const tCrit = Color(0xFFFBE9E7);
  static const tInfo = Color(0xFFE6F0F9);
  static const tOff = Color(0xFFE9EDEB);

  // Location colours, the same on every chart
  static const fs = Color(0xFF10795A);
  static const gp = Color(0xFF2F6FB0);
  static const nw = Color(0xFFC27C0E);

  static Color locationColour(String id) => switch (id) {
        'FS' => fs,
        'GP' => gp,
        'NW' => nw,
        _ => tx3,
      };

  static const shadow = [BoxShadow(color: Color(0x0D0D2620), blurRadius: 10, offset: Offset(0, 2))];
  static const shadowFloat = [BoxShadow(color: Color(0x240D2620), blurRadius: 26, offset: Offset(0, 10))];
  static const shadowButton = [BoxShadow(color: Color(0x3D10795A), blurRadius: 16, offset: Offset(0, 6))];
}

/// The app theme: Inter font, green brand colour, light canvas.
ThemeData buildTheme() {
  final base = ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(seedColor: Sg.brand, primary: Sg.brand, surface: Sg.canvas),
    scaffoldBackgroundColor: Sg.canvas,
  );
  return base.copyWith(
    textTheme: GoogleFonts.interTextTheme(base.textTheme).apply(bodyColor: Sg.tx, displayColor: Sg.tx),
    splashFactory: InkRipple.splashFactory,
  );
}

/// A short way to write a text style in Inter.
TextStyle sgText(double size, {FontWeight weight = FontWeight.w400, Color color = Sg.tx, double? spacing, double? height}) =>
    GoogleFonts.inter(fontSize: size, fontWeight: weight, color: color, letterSpacing: spacing, height: height);
