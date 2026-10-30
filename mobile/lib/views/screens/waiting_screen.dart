// SUMMARY: The "Waiting for the Firebase connection" screen, shown instead of the app while a Firebase setting is missing
// or when Firebase could not start. It lists the settings that are still empty.
// MVVM: View. It only draws the list it is given by main.dart; there is no data behind it.
import 'package:flutter/material.dart';

import '../../core/theme.dart';
import 'sign_in_screen.dart';

class WaitingForFirebaseApp extends StatelessWidget {
  const WaitingForFirebaseApp({super.key, required this.missing, this.problem});

  /// The names of the empty settings, for example FIREBASE_API_KEY.
  final List<String> missing;

  /// Set when Firebase was configured but could not start.
  final String? problem;

  @override
  Widget build(BuildContext context) => MaterialApp(
        title: 'SensorGrid',
        debugShowCheckedModeBanner: false,
        theme: buildTheme(),
        builder: phoneColumn,
        home: Scaffold(
          backgroundColor: Sg.deep,
          body: SingleChildScrollView(
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              const SignInHero(),
              Container(
                decoration: const BoxDecoration(color: Sg.canvas, borderRadius: BorderRadius.vertical(top: Radius.circular(32))),
                padding: const EdgeInsets.fromLTRB(24, 28, 24, 32),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text('SETUP', style: sgText(10, weight: FontWeight.w600, color: Sg.brand, spacing: 1.6)),
                  const SizedBox(height: 10),
                  Text('Waiting for the Firebase connection', style: sgText(24, weight: FontWeight.w700, spacing: -0.5)),
                  const SizedBox(height: 10),
                  Text(problem ?? 'The app is ready. It opens as soon as it is built with these settings:', style: sgText(14, color: Sg.tx2, height: 1.55)),
                  const SizedBox(height: 14),
                  for (final m in missing)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 6),
                      child: Row(children: [
                        Container(width: 8, height: 8, decoration: const BoxDecoration(color: Sg.nearLimit, shape: BoxShape.circle)),
                        const SizedBox(width: 10),
                        Text(m, style: sgText(13, weight: FontWeight.w600)),
                      ]),
                    ),
                  const SizedBox(height: 12),
                  Text('See "Connect Firebase" in the mobile README.md.', style: sgText(12.5, color: Sg.tx3, height: 1.55)),
                ]),
              ),
            ]),
          ),
        ),
      );
}

/// On a wide window (a tablet or the web preview) the app is kept to a phone-sized column in the middle.
Widget phoneColumn(BuildContext context, Widget? child) => ColoredBox(
      color: const Color(0xFFE8ECE9),
      child: Center(child: ConstrainedBox(constraints: const BoxConstraints(maxWidth: 480), child: child)),
    );
