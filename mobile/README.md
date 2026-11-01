# SensorGrid mobile (Flutter, Android first)

The app for **field officers** (role `Standard User`). It contains **no sample data**: it signs in with Firebase Authentication and
reads the same Firebase Realtime Database as the web console. Until it is built with the Firebase settings, it shows
**"Waiting for the Firebase connection"** and lists the settings that are missing.

## Run it

```bash
flutter pub get
flutter run
```

Without settings you see the "Waiting for the Firebase connection" screen. That is correct: the app is ready and waiting.

## Connect Firebase

1. In the Firebase console, add an **Android app** to the project with the package name `za.ac.cut.sensorgrid_mobile`.
2. Copy `firebase_config.example.json` to `firebase_config.json` in this folder (it is git-ignored) and fill it in from
   **Project settings > Your apps** (the Android app):
   - `FIREBASE_API_KEY`
   - `FIREBASE_APP_ID`
   - `FIREBASE_MESSAGING_SENDER_ID`
   - `FIREBASE_PROJECT_ID`
   - `FIREBASE_DATABASE_URL`
3. Deploy the database rules from `web/firebase` (see `web/README.md`). They keep each officer to their own provinces.
4. Run or build with the settings:
   ```bash
   flutter run --dart-define-from-file=firebase_config.json
   flutter build apk --release --dart-define-from-file=firebase_config.json
   ```

No `google-services.json` or `flutterfire configure` is needed. The app holds **no** service key: it works as the signed-in person,
under the Security Rules. Every connection point is marked `CONNECT FIREBASE HERE`
(`lib/core/app_config.dart`, `lib/main.dart`, `lib/repositories/repository_set.dart`).

## Who can sign in

| Account | What happens |
|---|---|
| Field officer, Active | Signs in and sees only their assigned provinces |
| Field officer, Invited | The first sign in finishes the invitation: the status becomes Active |
| Administrator | Refused with **"Use the web console"** |
| Deactivated | Refused with **"Access denied"** |

People are invited from **User accounts** on the web console; the same email and password work on the web and in this app.
A person who stays signed in is let straight in the next time the app opens, after the same checks.

## Architecture: MVVM (Model, View, ViewModel) with Provider

| Folder | Part | What is in it |
|---|---|---|
| `lib/models` | Model | Entities, enums, the four rules and the ready-to-show screen models. No Flutter, no Firebase |
| `lib/repositories` | Repository | The repository interfaces, `RepositorySet`, and the Firebase versions in `firebase/` |
| `lib/viewmodels` | ViewModel | One `ChangeNotifier` per screen, plus `SessionViewModel` (who is signed in) and `ViewModelFactory` |
| `lib/views` | View | The screens M1 to M9, the waiting screen, the shared widgets, the charts and the named routes |
| `lib/core` | Shared | Theme colours, text formatting, the Firebase settings |
| `lib/main.dart` | Wiring | Starts Firebase, `MultiProvider`, `onGenerateRoute` |

The MVVM rules this code keeps:

- A View never imports a repository or Firebase. It gets its ViewModel from `ViewModelFactory` and only calls its methods.
- A ViewModel never imports Flutter widgets (only `foundation.dart` for `ChangeNotifier`).
- A Model has no Flutter imports at all.
- Every rule is checked in the ViewModel before a write (for example the note in `AlertDetailViewModel.act`).
- After Acknowledge or Resolve, `SessionViewModel.dataChanged()` makes Home, Sites, Alerts, Reports and the tab badge reload.

Pages on top of the tabs are named routes in `lib/views/routes.dart`: `/profile`, `/location`, `/trend`, `/alert`.

## The four rules (in `lib/models/rules.dart`)

1. A value outside its band raises a **Breach** alert.
2. No reading for **1 minute** is **Offline** (the gateway sends a reading about every 6 seconds).
3. **Acknowledge and Resolve both need a note.**
4. A field officer sees **only assigned provinces** (checked in every ViewModel and by the Security Rules).

## What the app reads and writes

| Action | Database |
|---|---|
| Sign in | Firebase Authentication, then `users/{uid}` and `user_locations/{uid}`; Invited becomes Active; `last_active_at` |
| Home, Sites, Province detail | `locations`, `sensors`, `readings/{sensor_id}/latest`, `thresholds` |
| Trends | `readings/{sensor_id}/history` as a key range (the keys are push IDs, which start with their time). A new row about every 6 seconds |
| Alerts | `alerts/{loc}` and `alert_actions/{loc}`. Acknowledge and Resolve write the action and the new state in ONE update |
| Notifications | `notifications/{uid}`, marking `is_read` and `read_at` |
| Profile | `users/{uid}/notify_breach`, `notify_offline`, `notify_daily` |

The readings come from the third-party sensor gateway, which writes its own field names. The app maps them like this
(the web console does the same):

| Gateway field | Shown as |
|---|---|
| `timestamp` | the time of the reading |
| `moisture` | soil moisture, divided by 10 (281 shows as 28.1 %) |
| `temperature` | temperature, °C |
| `ec` | conductivity, µS/cm |
| `ph`, `nitrogen`, `phosphorus`, `potassium` | the same names |

The gateway sends no battery or signal, so the app shows neither. A field officer can read a sensor's readings only when the
web console has registered the sensor (it writes `sensor_locations/{sensor_id}`, which the Security Rules check).

The app never writes readings, alerts or accounts. Report files are generated and downloaded on the web console.

Packages are free ones from pub.dev only: `provider`, `intl`, `google_fonts`, `firebase_core`, `firebase_auth`, `firebase_database`.
