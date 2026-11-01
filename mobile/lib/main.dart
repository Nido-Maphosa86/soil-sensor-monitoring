// SUMMARY: Where the app starts. It connects to Firebase with the settings from lib/core/app_config.dart, creates the app-wide
// ViewModels, shares them with every screen through Provider, and sets up the named routes.
// While a setting is missing (or Firebase cannot start) the app shows "Waiting for the Firebase connection" instead.
// MVVM wiring:
//   Model       lib/models        plain data and the four rules
//   Repository  lib/repositories  Firebase Authentication and the Realtime Database
//   ViewModel   lib/viewmodels    one per screen; holds the screen state and calls the repositories
//   View        lib/views         the screens; they only draw what a ViewModel gives them
import 'package:firebase_auth/firebase_auth.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_database/firebase_database.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/app_config.dart';
import 'core/theme.dart';
import 'repositories/repository_set.dart';
import 'viewmodels/session_view_model.dart';
import 'viewmodels/view_model_factory.dart';
import 'views/routes.dart';
import 'views/screens/main_shell.dart';
import 'views/screens/sign_in_screen.dart';
import 'views/screens/waiting_screen.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // CONNECT FIREBASE HERE: the settings come from `flutter run --dart-define-from-file=firebase_config.json` (see README.md).
  final missing = FirebaseConfig.missing;
  if (missing.isNotEmpty) {
    runApp(WaitingForFirebaseApp(missing: missing));
    return;
  }
  try {
    final app = await Firebase.initializeApp(options: FirebaseConfig.options);
    if (FirebaseConfig.emulatorHost.isNotEmpty) {
      // Local testing with the Firebase Emulator Suite (firebase emulators:start --only auth,database).
      await FirebaseAuth.instanceFor(app: app).useAuthEmulator(FirebaseConfig.emulatorHost, 9099);
      FirebaseDatabase.instanceFor(app: app, databaseURL: FirebaseConfig.databaseUrl).useDatabaseEmulator(FirebaseConfig.emulatorHost, 9000);
    }
    runApp(SensorGridApp(repositories: RepositorySet.firebase(app)));
  } catch (e) {
    runApp(WaitingForFirebaseApp(missing: const [], problem: 'Firebase could not start: $e'));
  }
}

class SensorGridApp extends StatefulWidget {
  const SensorGridApp({super.key, required this.repositories});

  final RepositorySet repositories;

  @override
  State<SensorGridApp> createState() => _SensorGridAppState();
}

class _SensorGridAppState extends State<SensorGridApp> {
  // Made once for the whole life of the app.
  late final SessionViewModel _session = SessionViewModel(widget.repositories);
  late final ViewModelFactory _factory = ViewModelFactory(widget.repositories, _session);

  @override
  void dispose() {
    _session.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => MultiProvider(
        providers: [
          ChangeNotifierProvider<SessionViewModel>.value(value: _session),
          Provider<ViewModelFactory>.value(value: _factory),
        ],
        child: MaterialApp(
          title: 'SensorGrid',
          debugShowCheckedModeBanner: false,
          theme: buildTheme(),
          onGenerateRoute: AppRoutes.generate,
          builder: phoneColumn,
          // Nobody signed in: the sign-in screen (which first checks for a saved sign-in). Someone signed in: the four tabs.
          home: Consumer<SessionViewModel>(builder: (context, session, _) => session.user == null ? const SignInScreen() : const MainShell()),
        ),
      );
}
