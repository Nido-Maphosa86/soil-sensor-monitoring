# SensorGrid Mobile App

## Project Description

SensorGrid is a soil and environmental monitoring system for **pigeon pea crops** grown at sites in different South African provinces (for example Free State, FS).

Soil probes in the field measure **soil moisture, soil temperature, pH, electrical conductivity, nitrogen, phosphorus and potassium**. A sensor gateway sends a new reading from each probe about **every 6 seconds** to a **Firebase Realtime Database**.

The **mobile app** (this folder) is for **field officers**. It gives them, on their phone:
- the live readings of the provinces assigned to them;
- the trends of each value;
- the alerts, with Acknowledge and Resolve;
- in-app notifications when a new alert is raised.

The **web console** (in `../web`, ASP.NET Core MVC) uses the same database. That is where administrators set up sites, sensors, thresholds and user accounts, where the alerts are raised, and where report files are made.

The app holds no sample data. Everything shown comes from the live Firebase project.

## Project Objectives

1. **Put the field on the phone.** Show a field officer the current soil conditions of their provinces wherever they are.
2. **Stay live.** Readings, sensor states, alerts and notifications update by themselves, without pulling to refresh.
3. **Warn early.** Show a new alert at once: a banner on any screen, and a count on the bell.
4. **Act on alerts in the field.** Acknowledge and resolve an alert with a note; the web console sees it within seconds.
5. **Show only what is theirs.** A field officer sees only their assigned provinces, enforced by the app and by the database rules.
6. **Keep it simple and readable.** Plain numbers against their target bands; no advice text.

## Key Features

| Screen | What it does |
|---|---|
| **M1 Sign in / Forgot password** | Email and password through Firebase Authentication. A wrong email or password gets one message that does not say which part was wrong. The reset answer is the same whether or not the email exists. Administrators are told to use the web console; a deactivated account gets "Access denied". |
| **M2 Home** | A greeting, the sensor network (how many sensors report, one square per sensor), the newest open alert, and a card for each province. The **bell** shows the unread notifications. |
| **M3 Sites** | The person's provinces, each with its sensors reporting, readings in the last hour, the share in range and the current averages. |
| **M4 Province detail** | The headline soil moisture, then three tabs: **Readings** (each value against its band as a traffic light: in range, near limit, out of range), **Trends**, and **Sensors** (Online, Offline or Retired, and the last reading). With two or more sensors, a switch **Average | GP-01 | GP-02** under the header chooses whether the headline and the values show the average of the reporting sensors (the default) or one sensor, and tapping a value opens its trend on that sensor. Values that are not live (the chosen sensor or every sensor offline) are shown in grey as the last values received. |
| **M5 Trend** | One value of one sensor over **15 min** (live, a point per 10 s), **24 h**, **7 days** or **30 days**, with its band and the lowest, average and highest values. A period without readings says so and offers a longer one. |
| **M6 Alerts** | Counters (open, acknowledged, resolved), the filters All, Critical, Warning, Offline, Acknowledged and Resolved, and the alerts grouped by day. The open count is a badge on the Alerts tab. |
| **M7 Alert detail** | The reading against its threshold, the activity of the alert, and **Acknowledge** and **Resolve**. Both need a note (M7b). |
| **M8 Reports** | The nutrient levels (N, P, K) and the share of readings in range over the last 24 hours. Report files are made and downloaded on the web console. |
| **M9 Profile** | Name, email, role, the assigned provinces, the two notification switches (**Threshold breaches**, **Sensor offline**) and Sign out. |
| **M10 Notifications** | Opened from the bell: newest first, grouped by day, unread ones with a green dot. Tapping one opens the alert and marks it read; **Mark all read** clears them all. |
| **Banner** (every screen) | When a new notification arrives while the app is open, a banner drops in at the top with **View** and **Dismiss**. It goes away after 6 seconds. |

### Live updates

| What changes | Where it shows | How soon |
|---|---|---|
| A new reading (about every 6 s) | Province screen, 15-minute trend | Within 10 s |
| | Home and Sites cards | Within 30 s |
| A sensor stops sending | Grey **Offline** | About 1 minute after its last reading |
| A new alert | Banner, bell, Alerts tab and its badge | 1 to 2 s after the web console raises it |
| An alert acknowledged or resolved by anyone | Alerts tab, an open alert | About 2 s |
| A province given or taken away on the web | Every list | Within a few seconds, no sign-out needed |

Values are redrawn every 10 seconds rather than with every reading: each point is the average of the readings in those 10 seconds.

## System Architecture

```
 ┌──────────────┐   every ~6 s   ┌───────────────────────────────┐
 │ Soil probes  │ ─────────────▶ │ Sensor gateway (third party)  │
 └──────────────┘    (Modbus)    └───────────────┬───────────────┘
                                                 │ writes readings/{sensor}/...
                                                 ▼
                              ┌──────────────────────────────────────┐
                              │   Firebase Realtime Database         │
                              │   Firebase Authentication            │
                              └───────▲──────────────────────▲───────┘
                       REST + service │                      │ Firebase SDK: a live connection,
                       account token  │                      │ limited by the security rules
                 ┌────────────────────┴───────┐      ┌───────┴──────────────┐
                 │  SensorGrid web console    │      │  SensorGrid mobile   │
                 │  raises alerts every 10 s  │      │  app (this folder)   │
                 │  and writes notifications  │      │  field officers      │
                 └────────────────────────────┘      └──────────────────────┘
```

The app signs in as the field officer and talks to Firebase directly. Firebase **pushes** each change down a live connection, which is why the phone updates at once. Every read and write is limited by the security rules to the officer's own provinces.

### MVVM (Model, View, ViewModel) with Provider

```
   View          lib/views          the screens and widgets; they only draw what a ViewModel gives them
     │ asks ViewModelFactory for its ViewModel, and calls its methods
     ▼
   ViewModel     lib/viewmodels     one ChangeNotifier per screen; holds the screen state and checks the rules
     │
     ▼
   Repository    lib/repositories   interfaces, and their Firebase versions (Authentication and the Realtime Database)
     │
     ▼
   Model         lib/models         entities, enums, the four rules, the screen models; no Flutter, no Firebase
```

The rules this code keeps:
- A View never imports a repository or Firebase. It gets its ViewModel from `ViewModelFactory`.
- A ViewModel never imports Flutter widgets (only `foundation.dart` for `ChangeNotifier`).
- A Model has no Flutter imports at all.
- Every rule is checked in the ViewModel before a write (for example the note in `AlertDetailViewModel.act`).

The app-wide ViewModels, made once in `main.dart`:

| ViewModel | What it does |
|---|---|
| `SessionViewModel` | Who is signed in, and a "data changed" signal. It follows the person's provinces live. |
| `LiveSignals` | Listens to Firebase: the alerts of the person's provinces, the newest reading of each sensor, their notifications and their provinces. A 20-second heartbeat makes a silent sensor turn Offline. |
| `InboxViewModel` | The bell count and the banner. |
| `ViewModelFactory` | Makes each screen's ViewModel, connected to the repositories and the session. |

## Technologies Used

| Area | Technology |
|---|---|
| Language and framework | **Dart** and **Flutter** (stable channel), Material design |
| Platform | **Android** (package `za.ac.cut.sensorgrid_mobile`, app name **SensorGrid**) |
| Architecture | **MVVM** with **Provider** (`provider` 6.1.5+1) |
| Database | **Firebase Realtime Database** (`firebase_database` 13.0.0), live listeners |
| Sign-in | **Firebase Authentication**, email and password (`firebase_auth` 7.0.0, `firebase_core` 5.0.0) |
| Formatting and fonts | `intl` 0.20.3 (dates and numbers, South African time), `google_fonts` 8.2.1 (Inter) |
| Charts | Drawn with Flutter's own `CustomPainter` (`lib/views/widgets/charts.dart`), with no chart library |

All packages are free ones from pub.dev. No `google-services.json` and no `flutterfire configure` are needed.

## Prerequisites

- The **Flutter SDK** (stable, Dart 3.11 or newer). Check it with `flutter --version`.
- **Android Studio** with the Android SDK, or the Android command-line tools. Check everything with `flutter doctor`.
- An **Android phone** with Developer options and **USB debugging** switched on (or an Android emulator).
- **Git**.
- The project's **Firebase settings** (from the project owner), and a **field officer account** made on the web console.

## Installation and Setup

1. Get the code:
   ```bash
   git clone https://github.com/Nido-Maphosa86/soil-sensor-monitoring.git
   ```
2. Go to the app:
   ```bash
   cd soil-sensor-monitoring/mobile
   ```
3. Download the packages:
   ```bash
   flutter pub get
   ```
4. Check the code. It must say **No issues found!**:
   ```bash
   flutter analyze
   ```
5. Fill in the Firebase settings. See the next section.

Without the settings the app still starts, and shows **"Waiting for the Firebase connection"** with the list of what is missing. That is correct: the app is ready and waiting.

## Firebase Configuration

### 1. In the Firebase console (once per project)

1. **Authentication > Sign-in method:** enable **Email/Password** (shared with the web console).
2. **Project settings > General > Add app > Android**, with the package name `za.ac.cut.sensorgrid_mobile`. Register it; skip the download steps.
3. **Realtime Database > Rules:** paste all of `web/firebase/database.rules.json` and click **Publish**. The app can read nothing until the rules are published.

### 2. The settings of the app

Copy `firebase_config.example.json` to `firebase_config.json` in the `mobile` folder, and fill it in from **Project settings > Your apps** (the Android app):

| Setting | What to put in it |
|---|---|
| `FIREBASE_API_KEY` | The Android app's API key |
| `FIREBASE_APP_ID` | The Android app's App ID |
| `FIREBASE_MESSAGING_SENDER_ID` | The project number |
| `FIREBASE_PROJECT_ID` | The project ID |
| `FIREBASE_DATABASE_URL` | For example `https://<project-id>-default-rtdb.firebaseio.com` |
| `FIREBASE_EMULATOR_HOST` | Leave empty. Only for local testing with the Firebase emulators. |

`firebase_config.json` is git-ignored: never commit it. The settings are passed to the app when it is built (`--dart-define-from-file`). Every connection point is marked `CONNECT FIREBASE HERE` (`lib/core/app_config.dart`, `lib/main.dart`, `lib/repositories/repository_set.dart`).

The app holds **no** service key. It works only as the signed-in person, under the security rules.

### 3. Accounts

Field officers are invited on the web console (**User accounts > Invite user**) and given their provinces. The same email and password work on the web and in the app.

| Account | What happens in the app |
|---|---|
| Field officer, Active | Signs in and sees only their assigned provinces |
| Field officer, Invited | The first sign-in finishes the invitation: the status becomes Active |
| Administrator | Refused with **"Use the web console"** |
| Deactivated | Refused with **"Access denied"** |

A person who stays signed in is let straight in the next time the app opens, after the same checks.

## Database Structure

The app uses the same JSON tree as the web console (see `web/README.md` for the whole tree). This is what the app reads and writes:

| Action | Database |
|---|---|
| Sign in | Firebase Authentication, then `users/{uid}` and `user_locations/{uid}`; an Invited account becomes Active; `last_active_at` |
| Home, Sites, Province detail | `locations`, `sensors`, `thresholds`, `readings/{sensor_id}/latest`, and the last hour of `readings/{sensor_id}/history` |
| Trend | `readings/{sensor_id}/history` as a key range. **15 min** reads every row (about 150) and averages each 10 s; **24 h**, **7 days** and **30 days** read ONE row per 5 minutes, hour and 6 hours |
| Reports | One row per hour per sensor over the last 24 hours |
| Alerts | `alerts/{location_id}` and `alert_actions/{location_id}`. Acknowledge and Resolve write the action and the new state in ONE update |
| Notifications | `notifications/{uid}`, read live; marking `is_read` and `read_at` (one when its alert is opened, or all in one update) |
| Profile | `users/{uid}/notify_breach` and `notify_offline` |
| Live listeners | `alerts/{location_id}`, `readings/{sensor_id}/latest`, `notifications/{uid}`, `user_locations/{uid}` |

- Times are **UTC milliseconds**, shown in **South African time** (UTC+2).
- The reading keys are Firebase push IDs, which start with their time, so a period is a key range. The app never reads a whole readings branch.
- The app **never writes** readings, sensors, thresholds, sites or accounts, and **never creates** alerts or notifications: the gateway and the web console do.

### The four rules (`lib/models/rules.dart`)

1. A value outside its band is a **breach**. "Near limit" (amber) is within 10 % of the band width from either edge.
2. No reading for **1 minute** is **Offline** (the same limit as the web console).
3. **Acknowledge and Resolve both need a note.** An alert only moves forward: Open → Acknowledged → Resolved, or Open → Resolved.
4. A field officer sees **only assigned provinces**: checked in every ViewModel and by the security rules.

## Sensor Data Mapping

The gateway is built by a third party. It writes each probe's readings under the probe's **sensor ID**:

```
readings / FS-01 / history / -Nx3k...   ← a new row about every 6 seconds (push ID = time order)
readings / FS-01 / latest               ← a copy of the newest row
```

The app reads the rows as they are (the web console does the same):

| Gateway field | Shown in the app as | Unit | Note |
|---|---|---|---|
| `timestamp` | Time of the reading | UTC milliseconds | |
| `moisture` | Soil moisture | % | **Divided by 10** (281 shows as 28.1 %) |
| `temperature` | Temperature | °C | |
| `ec` | Conductivity | µS/cm | |
| `ph` | pH | | |
| `nitrogen` | Nitrogen (N) | mg/kg | |
| `phosphorus` | Phosphorus (P) | mg/kg | |
| `potassium` | Potassium (K) | mg/kg | |

Important:
- **A sensor must be registered on the web console** with exactly the gateway's code (for example `FS-01`). Registering writes `sensor_locations/FS-01 = "FS"`, which the security rules use to let the officers of that province read its readings.
- **A row of zeros** (the probe not in the soil) is left out of every figure and chart.
- **A retired sensor** (retired on the web console) shows as **Retired** and is left out of every figure.
- The gateway sends no battery or signal values, so the app does not show them.

## How to Run the Application

With the phone connected by USB (or an emulator running), from the `mobile` folder:

```bash
flutter run --dart-define-from-file=firebase_config.json
```

To build the release app and install it on the phone:

```bash
flutter build apk --release --dart-define-from-file=firebase_config.json
```
```bash
flutter install
```

The app then works without the cable, over Wi-Fi or mobile data. The APK is at `build/app/outputs/flutter-apk/app-release.apk` and can also be copied to a phone and installed by hand.

### Notifications, and what they need

- The **web console raises the alerts** (every 10 seconds) and writes the notifications. On Render's free plan it sleeps after 15 minutes without visitors, and while it sleeps no alerts are raised. A free UptimeRobot monitor keeps it awake.
- A notification is written for each field officer **assigned to that province** who has that kind **switched on** in their profile. The Alerts tab always lists every alert.
- Notifications show **while the app is open** (banner and bell). Nothing pops up while the app is closed or the phone is locked; that would need push notifications (Firebase Cloud Messaging), which this app does not use.

### Something is wrong?

| Problem | What to do |
|---|---|
| "Waiting for the Firebase connection" | A setting is missing. The screen names it. Check `firebase_config.json` and build with `--dart-define-from-file`. |
| "Use the web console" | The account is an Administrator. The app is for field officers. |
| "You do not have access to this" | The database rules are not published, or no province is assigned. Publish the rules; assign provinces on the web console. |
| A province shows no sensors | No sensor is registered there yet (web console, Register sensor). |
| A sensor shows **Offline** | Nothing for 1 minute. Check the gateway, and that the registered code matches the gateway's exactly. |
| No banner or bell count | The web console must be awake to raise alerts, and the switch for that kind must be on in Profile. |
| The app cannot reach Firebase on the phone | Check the phone's internet. The release build includes the internet permission. |

## Project Structure

```
mobile/
├── pubspec.yaml                     the packages
├── firebase_config.example.json     the settings to copy to firebase_config.json (git-ignored)
├── android/                         the Android app (package za.ac.cut.sensorgrid_mobile, name and icon)
└── lib/
    ├── main.dart                    start-up: Firebase, the app-wide ViewModels, Provider, routes, the banner
    ├── core/                        app_config.dart (the Firebase settings), format.dart (times and numbers), theme.dart (colours and text)
    ├── models/
    │   ├── entities.dart            AppUser, Location, Sensor, Reading, Threshold, AlertItem, AppNotification...
    │   ├── enums.dart               roles, states, alert kinds, parameters, trend ranges
    │   ├── rules.dart               the four rules
    │   └── screen_models.dart       the ready-to-show data of each screen
    ├── repositories/
    │   ├── repositories.dart        the interfaces (auth, locations, readings, alerts, profile, clock)
    │   ├── repository_set.dart      one box holding every repository
    │   └── firebase/                the Firebase versions
    ├── viewmodels/
    │   ├── base_view_model.dart     loading, ready or failed; quiet live reloads
    │   ├── session_view_model.dart  who is signed in; follows the provinces live
    │   ├── live_signals.dart        the live Firebase listeners and the heartbeat
    │   ├── inbox_view_model.dart    the bell count and the banner
    │   ├── notification_feed.dart   the notification list, shared by the bell and the inbox
    │   ├── location_insights.dart   the shared maths of the province cards
    │   ├── view_model_factory.dart  makes each screen's ViewModel
    │   └── ...                      one ViewModel per screen (sign in, home, sites, province, trend, alerts, alert, reports, profile, notifications)
    └── views/
        ├── routes.dart              /profile, /location, /trend, /alert, /notifications
        ├── screens/                 M1 to M10, the main frame with the four tabs, and the waiting screen
        └── widgets/                 the shared widgets, the charts, the alert texts and the notification banner
```

## Security and Database Rules

### In the app

- **Sign-in:** Firebase Authentication holds the passwords. The app never stores them.
- **Field officers only:** an Administrator or a deactivated account is signed out again at once, with a clear message.
- **No keys in the app:** the app has no service-account key. The Firebase settings in `firebase_config.json` only identify the project; access comes from the person's sign-in and the security rules.
- **Provinces:** every ViewModel checks rule 4 before reading. A province taken away on the web console disappears from the app within seconds.
- **Notes:** Acknowledge and Resolve are refused without a note, in the app and in the rules.
- **Nothing written that isn't the officer's:** the app only writes its own account status (once), its notification switches and read marks, and alert actions.

### The Firebase Realtime Database rules (`web/firebase/database.rules.json`)

These rules are what protect the database from the app; the web console's service account is not affected by them.

- **Everything is closed by default.**
- **Only field officers** ("Standard User", Active or Invited) get any access.
- A field officer can **read**:
  - their own `users`, `user_locations` and `notifications`;
  - `locations`, `sensors`, `thresholds`, `alerts` and `alert_actions` of the provinces given to them;
  - `readings/{sensor}` only when `sensor_locations/{sensor}` points to one of their provinces.
- A field officer can **write** only:
  - their own `account_status` (Invited → Active, once), `last_active_at`, `notify_breach` and `notify_offline`;
  - an alert's `state`, forward only;
  - a new `alert_actions` entry, for their own user ID, with a note that is not empty and at most 500 characters;
  - `is_read` and `read_at` of their own notifications.
- **Nobody** can write readings, sensors, thresholds, sites or accounts through the rules.

After any change to the rules, paste the whole file into **Firebase console > Realtime Database > Rules** and click **Publish**.
