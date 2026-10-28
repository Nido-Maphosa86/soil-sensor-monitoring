// SUMMARY: The Firebase connection settings of the app, and the South African time offset.
// CONNECT FIREBASE HERE: the settings are passed in when the app is built, from a file that is NOT in the repository:
//   flutter run --dart-define-from-file=firebase_config.json
// Copy firebase_config.example.json to firebase_config.json and fill in the values from the Firebase console
// (Project settings > Your apps > the Android app). Until every value is filled in, the app shows "Waiting for the Firebase connection".
import 'package:firebase_core/firebase_core.dart';

class FirebaseConfig {
  static const apiKey = String.fromEnvironment('FIREBASE_API_KEY');
  static const appId = String.fromEnvironment('FIREBASE_APP_ID');
  static const messagingSenderId = String.fromEnvironment('FIREBASE_MESSAGING_SENDER_ID');
  static const projectId = String.fromEnvironment('FIREBASE_PROJECT_ID');
  static const databaseUrl = String.fromEnvironment('FIREBASE_DATABASE_URL');

  /// Local testing only: the address of the Firebase Emulator Suite, for example "10.0.2.2" from the Android emulator.
  /// When set, sign-in uses port 9099 and the database port 9000 on that host.
  static const emulatorHost = String.fromEnvironment('FIREBASE_EMULATOR_HOST');

  /// The names of the settings that are still empty. An empty list means the app can connect.
  static List<String> get missing => [
        if (apiKey.isEmpty) 'FIREBASE_API_KEY',
        if (appId.isEmpty) 'FIREBASE_APP_ID',
        if (messagingSenderId.isEmpty) 'FIREBASE_MESSAGING_SENDER_ID',
        if (projectId.isEmpty) 'FIREBASE_PROJECT_ID',
        if (databaseUrl.isEmpty) 'FIREBASE_DATABASE_URL',
      ];

  static FirebaseOptions get options => const FirebaseOptions(
        apiKey: apiKey,
        appId: appId,
        messagingSenderId: messagingSenderId,
        projectId: projectId,
        databaseURL: databaseUrl,
      );
}

class AppConfig {
  /// South African Standard Time is UTC+2 (no daylight saving). Times are stored in UTC and shown in South African time.
  static const Duration southAfricaOffset = Duration(hours: 2);
}
