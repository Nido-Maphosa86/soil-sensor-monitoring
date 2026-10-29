// SUMMARY: One box that holds every repository the app uses, all connected to Firebase.
// MVVM: Repository. main.dart builds ONE RepositorySet after Firebase has started and shares it through the ViewModelFactory;
// every ViewModel takes what it needs from it. There is no sample data: everything comes from Firebase.
import 'package:firebase_auth/firebase_auth.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_database/firebase_database.dart';

import '../core/app_config.dart';
import 'firebase/firebase_repositories.dart';
import 'repositories.dart';

class RepositorySet {
  const RepositorySet({
    required this.clock,
    required this.auth,
    required this.locations,
    required this.readings,
    required this.alerts,
    required this.profile,
    required this.reports,
  });

  final Clock clock;
  final AuthRepository auth;
  final LocationRepository locations;
  final ReadingRepository readings;
  final AlertRepository alerts;
  final ProfileRepository profile;
  final ReportRepository reports;

  /// CONNECT FIREBASE HERE: uses the Firebase app started in main.dart with the settings from lib/core/app_config.dart.
  factory RepositorySet.firebase(FirebaseApp app) {
    final backend = FirebaseBackend(FirebaseAuth.instanceFor(app: app), FirebaseDatabase.instanceFor(app: app, databaseURL: FirebaseConfig.databaseUrl));
    final locations = FirebaseLocationRepository(backend);
    return RepositorySet(
      clock: SystemClock(),
      auth: FirebaseAuthRepository(backend),
      locations: locations,
      readings: FirebaseReadingRepository(backend, locations),
      alerts: FirebaseAlertRepository(backend),
      profile: FirebaseProfileRepository(backend),
      reports: FirebaseReportRepository(),
    );
  }
}
