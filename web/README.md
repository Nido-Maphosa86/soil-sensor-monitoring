# SensorGrid Web Console

## Project Description

SensorGrid is a soil and environmental monitoring system for **pigeon pea crops** grown at sites in different South African provinces (for example Free State, FS).

Soil probes in the field measure **soil moisture, soil temperature, pH, electrical conductivity, nitrogen, phosphorus and potassium**. A sensor gateway sends a new reading from each probe about **every 6 seconds** to a **Firebase Realtime Database**.

The **web console** (this folder) is where the Department of Agriculture works with that data:
- administrators set up sites, sensors, thresholds and user accounts;
- field officers follow the readings and alerts of their provinces.

A separate **mobile app** (Flutter, in `../mobile`) uses the same database. It gives field officers the readings, trends and alerts on their phone.

The console holds no sample data. Everything shown comes from the live Firebase project.

## Project Objectives

1. **Monitor soil conditions live.** Show the latest readings of every sensor, and their trends over 15 minutes, 24 hours, 7 days and 30 days.
2. **Warn early.** Raise an alert when a reading goes outside the safe band for the crop, or when a sensor stops reporting, so a field officer can act before the crop suffers.
3. **Keep a record of every alert.** An alert is acknowledged and resolved with a note. Who acted, and when, is saved.
4. **Give each person only their own data.** A field officer sees only the provinces given to them.
5. **Report.** Produce monthly summaries, breach logs and raw readings as PDF or CSV files.
6. **Be ready to grow.** New sites and sensors are added from the console, without changing any code.

## Key Features

| Page | What it does |
|---|---|
| **Sign in / Forgot password** | Email and password sign-in through Firebase Authentication. A password reset link is sent by email. The page gives the same answer whether or not the email exists. |
| **Dashboard** | The headline figures (sensors online, open alerts, values in range), and a trend chart per province: 15 minutes (live, a point every 10 seconds), 24 hours, 7 days or 30 days. The range switches without reloading the page. |
| **Sites** | Every site with its sensors, status and latest values. Administrators can **add a site**. |
| **Site detail** | One site: a live gauge per parameter (refreshed every 10 s) with its threshold band, and its sensors. With two or more sensors, a switch **Average | FS-01 | FS-02** chooses whether the gauges show the average of the reporting sensors (the default) or one sensor; a line says which ("Average of 2 sensors" or "FS-01 · Online · last reading 14:32"). When nothing reports, the gauges show the last values received in grey. |
| **Edit site** (administrator) | Change the name, crop, coordinates, planting date and description (the code cannot change). **Delete** a site once it has no sensors: its thresholds and alerts are removed and it is taken off every user, after typing its code to confirm. |
| **Sensors** | Every sensor: Online, Offline or Retired, and the time of its last reading. |
| **Register sensor** (administrator) | Adds a sensor to a site. It proposes the next free code (for example FS-02) and Modbus slave ID. |
| **Edit sensor** (administrator) | Change the installation date, depth, Modbus slave ID and "include in average" (the code and site cannot change). **Retire** a probe taken out of the field: it stops counting and raising alerts, its open alerts are resolved with a note, and it can be reactivated. **Delete** a retired sensor after typing its code: the registration and its alerts are removed, and **its readings are kept**. |
| **Readings log** | Filter by province, sensor and date range. Pages through the readings (Newest, Newer, Older). **Export CSV** covers up to 7 days. |
| **Thresholds** (administrator) | The minimum and maximum of each parameter for each site, with the reason (basis). Who changed it, and when, is saved. |
| **Alerts** | Filter chips: All, Critical, Warning, Offline, Acknowledged, Resolved. **Acknowledge** and **Resolve** need a note. **View note** on resolved alerts. |
| **Bell panel** | The person's notifications, with an unread dot and *Mark as read*. |
| **Reports** | Generate a report for a month and one or more provinces: **monthly summary** (one row per day, PDF or CSV), **breach log**, or **raw readings** (CSV, up to 7 days). The file is built when it is downloaded. |
| **User accounts** (administrator) | Invite a user (they get an email to set their password), set the role, give them one or more provinces, and deactivate or reactivate them. |
| **Profile** | Name, email and role, the notification switches (threshold breaches, sensor offline), change password, and sign out. |

### The alert rules

The **alert evaluator** runs inside the web console and checks the newest readings every **10 seconds**.

| Alert | When it is raised | Severity |
|---|---|---|
| **Breach** | A reading is below the minimum or above the maximum of its threshold. | **Critical** when the value is 15 % or more past the limit. Otherwise **Warning**. |
| **Offline** | A sensor has sent nothing for **1 minute**. | Critical (shown under the **Offline** chip) |

- An alert that is already open is never raised a second time.
- A row of zeros means the probe is not in the soil. It is shown, but it never raises a breach.
- Each new alert creates a notification for every user of that province who has that type switched on.
- An alert only moves forward: **Open → Acknowledged → Resolved**, or **Open → Resolved**.

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
                       REST + service │                      │ Firebase SDK +
                       account token  │                      │ security rules
                 ┌────────────────────┴───────┐      ┌───────┴──────────────┐
                 │  SensorGrid web console    │      │  SensorGrid mobile   │
                 │  (ASP.NET Core MVC)        │      │  app (Flutter, MVVM) │
                 │  + alert evaluator (10 s)  │      │  field officers      │
                 └────────────▲───────────────┘      └──────────────────────┘
                              │ browser
                    administrators, field officers
```

### Onion Architecture

The web console is one ASP.NET Core project, split into layers by folder. Each layer depends only on the layers inside it:

```
   Controllers + Views  (Web)        pages, forms, sign-in cookie
            │ calls interfaces only
            ▼
        Service                       what the app does: dashboard, alerts, reports, users; the alert evaluator
            │
            ▼
        Repository                    reads and writes the database; Firebase sign-in
            │
            ▼
        Domain                        entities and business rules; depends on nothing
```

- Controllers and views never touch the database. They call service interfaces (`IAlertService`, `IReportService`, ...).
- All database access goes through **one interface**, `IRealtimeDatabaseStore`, with these calls:
  - `GetAsync`
  - `QueryByKeyAsync` (key ranges and `limitToLast`)
  - `SetAsync`
  - `UpdateAsync` (a multi-path update: all or nothing)
  - `DeleteAsync`
- The web server signs in to Firebase with a **service account**. That account is not limited by the security rules, so **every service checks the person's role and provinces before every read and write**.

## Technologies Used

| Area | Technology |
|---|---|
| Language and framework | C#, **ASP.NET Core MVC on .NET 10**, Razor views |
| Database | **Firebase Realtime Database**, used through its REST API with `HttpClient` |
| Sign-in | **Firebase Authentication** (Identity Toolkit REST API), and cookie authentication with role and province claims |
| Server access to Firebase | A Google service account: an RS256 JWT signed with `System.Security.Cryptography`, exchanged for an OAuth access token |
| Front end | HTML, CSS and plain JavaScript. The charts are drawn as SVG by `wwwroot/js/site.js`, with no chart library. |
| Files | Its own CSV and PDF writers (`Service/Support/Writers.cs`), with no outside library |
| Hosting | **Docker** (`Dockerfile`) on **Render**. UptimeRobot can keep the free plan awake. |
| Mobile app (separate) | Flutter and Dart, MVVM, the Firebase SDK |

No NuGet packages are needed. The project uses only what comes with .NET.

## Prerequisites

- The **.NET 10 SDK**. Check it with `dotnet --version` (it should show 10.x).
- **Git**.
- Access to the project's **Firebase project**, as an owner or editor, so you can download a service-account key.
- A browser (Chrome, Edge or Firefox).
- Optional: **Docker**, to build the same image the host uses.

## Installation and Setup

1. Get the code:
   ```bash
   git clone https://github.com/Nido-Maphosa86/soil-sensor-monitoring.git
   ```
2. Go to the web project:
   ```bash
   cd soil-sensor-monitoring/web/src/SensorGrid.Web
   ```
3. Build it. It must say **Build succeeded**:
   ```bash
   dotnet build
   ```
4. Fill in the Firebase settings. See the next section.

## Firebase Configuration

### 1. In the Firebase console (once per project)

1. **Authentication > Sign-in method:** enable **Email/Password**.
2. **Build > Realtime Database:** create the database.
3. **Realtime Database > Rules:** paste all of `web/firebase/database.rules.json` and click **Publish**. This also adds the index on `alerts` (`state`, `raised_at`).
4. **Project settings > Service accounts > Generate new private key.** Save the JSON file **outside the repository**, for example `C:\keys\sensorgrid-admin.json`. Never commit it, and never send it to anyone.
5. **Project settings > General:** note the **Project ID** and the **Web API key**. The **Database URL** is shown at the top of the Realtime Database page.

### 2. The settings of the web console

The settings are read from the `SensorGrid` section. Keep them out of `appsettings.json`: on your computer use **user-secrets**, and on the host use environment variables.

| Setting | What to put in it |
|---|---|
| `SensorGrid:Firebase:ProjectId` | The Firebase project ID |
| `SensorGrid:Firebase:DatabaseUrl` | For example `https://<project-id>-default-rtdb.firebaseio.com` |
| `SensorGrid:Firebase:WebApiKey` | The Web API key |
| `SensorGrid:Firebase:ServiceAccountKeyPath` | The full path of the service-account JSON file |
| `SensorGrid:WebAppUrl` | The address of the console, used in invitation and reset emails (for example `http://localhost:5300`) |
| `SensorGrid:RunAlertEvaluatorInWebApp` | `true` (the default) runs the alert evaluator in this copy. Set it to `false` on a computer when the hosted copy already does it. |
| `SensorGrid:FirstAdministrator:Email` / `:Name` | Only the first time, on an empty database. See below. |

Run these in `web/src/SensorGrid.Web`, with your own values:

```bash
dotnet user-secrets set "SensorGrid:Firebase:ProjectId" "your-project-id"
```
```bash
dotnet user-secrets set "SensorGrid:Firebase:DatabaseUrl" "https://your-project-id-default-rtdb.firebaseio.com"
```
```bash
dotnet user-secrets set "SensorGrid:Firebase:WebApiKey" "your-web-api-key"
```
```bash
dotnet user-secrets set "SensorGrid:Firebase:ServiceAccountKeyPath" "C:\keys\sensorgrid-admin.json"
```
```bash
dotnet user-secrets set "SensorGrid:WebAppUrl" "http://localhost:5300"
```

Check them with:

```bash
dotnet user-secrets list
```

If a setting is missing, the console starts on a **"Waiting for the Firebase connection"** page that names the missing setting.

### 3. The first administrator (only on an empty database)

```bash
dotnet user-secrets set "SensorGrid:FirstAdministrator:Email" "you@example.org"
```
```bash
dotnet user-secrets set "SensorGrid:FirstAdministrator:Name" "Your Name"
```

Start the console. Firebase emails that address a link to set the password. Sign in, then:
1. **Sites > Add site:** add each site with its two-letter code (for example `FS`).
2. **Sensors > Register sensor:** register each probe with **exactly** the code the gateway uses (for example `FS-01`).
3. **Thresholds:** check the minimum and maximum of every parameter.
4. **User accounts > Invite user:** invite the field officers and give them their provinces.

### 4. Hosting on Render

The `web/Dockerfile` builds the console. On Render:
1. Create a **Web Service** from the repository, with **Root directory** `web` and **Runtime** Docker.
2. Under **Environment**, add the settings as environment variables. Use a double underscore instead of the colon, for example `SensorGrid__Firebase__ProjectId`.
3. Add the service-account JSON as a **Secret File** (Render puts it in `/etc/secrets/`). Set `SensorGrid__Firebase__ServiceAccountKeyPath` to `/etc/secrets/<file name>`.
4. Set `SensorGrid__WebAppUrl` to the Render address, for example `https://sensorgrid.onrender.com`.

On the free plan, Render sleeps after 15 minutes without visitors, and while it sleeps no alerts are checked. A free **UptimeRobot** monitor on `https://<your-app>.onrender.com/Account/Login` every 5 minutes keeps it awake.

## Database Structure

All of the data is one JSON tree in the Realtime Database.
- Field names are `snake_case`.
- Times are **UTC milliseconds**, and dates are `yyyy-MM-dd`.
- Empty values are not stored.
- New record IDs are Firebase push IDs, which are time-ordered and case-sensitive.

```
users/{uid}                              name, email, role ("Administrator" | "Standard User"),
                                         account_status (Active | Invited | Deactivated),
                                         created_at, last_active_at, notify_breach, notify_offline
user_locations/{uid}/{location_id}       true        ┐ always written together,
location_users/{location_id}/{uid}       true        ┘ in one update

locations/{location_id}                  name, description, crop, latitude, longitude, planting_date, created_at
sensors/{location_id}/{sensor_id}        name, model, is_active, include_in_average, installation_date,
                                         depth_cm, modbus_slave_id
sensor_locations/{sensor_id}             "{location_id}"   (which site a sensor belongs to)

readings/{sensor_id}/history/{push_id}   written by the gateway (see Sensor Data Mapping)
readings/{sensor_id}/latest              the newest reading, written by the gateway

thresholds/{location_id}/{parameter}     min_value, max_value, basis, updated_by, updated_at
                                         parameter: soil_moisture, soil_temperature, conductivity,
                                                    ph, nitrogen, phosphorus, potassium

alerts/{location_id}/{alert_id}          sensor_id, alert_type (Breach | Offline), parameter,
                                         measured_value, threshold_value,
                                         severity (Critical | Warning),
                                         state (Open | Acknowledged | Resolved), raised_at
alert_actions/{location_id}/{alert_id}/{acknowledged | resolved}
                                         user_id, note, acted_at
notifications/{uid}/{alert_id}           location_id, created_at, is_read, read_at

daily_stats/{sensor_id}/{yyyy-MM-dd}     readings, in_range, first_at, last_at, longest_gap_ms, offline_ms,
                                         {parameter}_min, {parameter}_sum, {parameter}_max
reports/{report_id}                      generated_by, name, format (PDF | CSV), report_type,
                                         period_start, period_end, generated_at
report_locations/{report_id}/{location_id}   true
```

Who writes what:
- **The gateway** is the only writer of `readings`.
- **The alert evaluator** is the only creator of `alerts` and `notifications`.
- **Acknowledge and Resolve** write `alert_actions` and the alert's `state` in **one** multi-path update. Registering a sensor writes `sensors` and `sensor_locations` together in the same way.
- `daily_stats` holds each sensor's figures per day, so a monthly report does not have to read hundreds of thousands of raw rows.

## Sensor Data Mapping

The gateway is built by a third party. It writes each probe's readings under the probe's **sensor ID**:

```
readings / FS-01 / history / -Nx3k...   ← a new row about every 6 seconds (push ID = time order)
readings / FS-01 / latest               ← a copy of the newest row
```

The console reads the rows as they are:

| Gateway field | Shown in the console as | Unit | Note |
|---|---|---|---|
| `timestamp` | Time of the reading | UTC milliseconds | |
| `moisture` | Soil moisture | % | **Divided by 10** (281 shows as 28.1 %), set by `SensorGrid:GatewayMoistureDivisor` |
| `temperature` | Soil temperature | °C | |
| `ec` | Conductivity | µS/cm | |
| `ph` | pH | | |
| `nitrogen` | Nitrogen (N) | mg/kg | |
| `phosphorus` | Phosphorus (P) | mg/kg | |
| `potassium` | Potassium (K) | mg/kg | |

Important:
- **The sensor ID links everything.** The gateway sends no location. The console finds the site through the registered sensor (`sensor_locations/FS-01 = "FS"`), so the code registered on *Register sensor* must match the gateway's code **exactly**, including capital letters and the dash.
- **The code format** is the site code, a dash and two digits (`FS-01`, `GP-01`, `NW-01`). It is unique and cannot be changed. A Modbus slave ID only has to be unique within its site.
- **Offline** means no new row for 1 minute.
- **A row of zeros** (moisture, conductivity, N, P and K all 0) means the probe is not in the soil. It is shown but never raises a breach.
- The gateway sends no battery or signal values, so the console does not show them.

## How to Run the Application

On your computer:

```bash
cd web/src/SensorGrid.Web
```
```bash
dotnet run
```

Open **http://localhost:5300** and sign in. To stop the console, press **Ctrl + C** in the terminal.

With Docker, from the `web` folder (pass the settings as environment variables, and mount the key file):

```bash
docker build -t sensorgrid-web .
```

The hosted copy is at the Render address, for example `https://sensorgrid.onrender.com`.

### Something is wrong?

| Problem | What to do |
|---|---|
| "Waiting for the Firebase connection" | A setting is missing. The page names it. Run `dotnet user-secrets list`. |
| "The file it points to does not exist" | `ServiceAccountKeyPath` is wrong. |
| A sensor shows **Offline** | Nothing for 1 minute. Check that the gateway is running and the sensor code matches exactly. |
| A sensor has no readings at all | It is registered with a different code from the gateway's. |
| Every value shows 0 % in range | Check the bands on the **Thresholds** page. |
| Port 5300 is already in use | Another copy is running. Stop it with **Ctrl + C**. |

## Project Structure

```
web/
├── Dockerfile                    builds the console for hosting
├── SensorGrid.sln
├── firebase/
│   ├── database.rules.json       the security rules (publish in the Firebase console)
│   └── firebase.json             emulator settings
└── src/SensorGrid.Web/
    ├── Program.cs                start-up: settings, layers, sign-in cookie, alert evaluator
    ├── appsettings.json          default settings (no secrets)
    ├── Domain/                   Entities.cs, Enums.cs, Rules.cs: the business rules
    ├── Repository/
    │   ├── Interfaces.cs         the repository interfaces
    │   ├── Repositories.cs       one repository per area, written against IRealtimeDatabaseStore
    │   ├── RepositoryLayer.cs    registers the repository layer
    │   ├── Settings.cs           the SensorGrid settings
    │   ├── Store/                IRealtimeDatabaseStore, the caching store, push IDs, JSON helpers
    │   ├── Firebase/             the REST store and the Google access-token provider
    │   └── Auth/                 Firebase Authentication (sign in, reset, invite, change password)
    ├── Service/
    │   ├── Interfaces.cs         the service interfaces
    │   ├── Services/             dashboard, sites, sensors, readings, thresholds, alerts, reports, users, profile
    │   ├── Dtos/                 what the services hand to the pages
    │   └── Support/              daily figures, the CSV and PDF writers
    ├── Filters/                  the alert evaluator worker; access and account checks
    ├── Controllers/              Account, Setup, Monitoring (dashboard, sites, sensors, readings, alerts),
    │                             Management (thresholds, reports, users, profile)
    ├── ViewModels/               the page models
    ├── Views/                    the Razor pages
    └── wwwroot/                  site.css, site.js (menus, dialogs, charts), favicon
```

## Security and Database Rules

### In the web console

- **Sign-in:** Firebase Authentication holds the passwords. The SensorGrid database never stores them.
- **The cookie:** after sign-in, the console reads `users/{uid}` and issues a cookie with the role and the provinces.
- **The account is checked on every request.** A deactivated account is signed out at once and sees an "Access denied" page.
- **Roles are enforced on the server.** A field officer who opens an administrator page (Thresholds, User accounts, Register sensor) is refused, not just shown a hidden menu.
- **Provinces are enforced in the Service layer.** This matters because the service account is not limited by the Firebase rules: every service checks the role and the assigned provinces before every read and write.
- **Input:** every form is validated on the server and protected with an anti-forgery token. Errors are shown inside the form's dialog.
- **Passwords:** at least 8 characters, with an upper-case letter, a lower-case letter, a number and a symbol.
- **No public sign-up.** Only an administrator can invite users.
- **Forgot password** gives the same answer whether or not the email exists.
- **Secrets:** the key file and the settings live in user-secrets or the host's environment, never in the repository or the browser. `.gitignore` blocks key files. If a key is ever exposed, delete it in Firebase (*Service accounts > Manage keys*) and generate a new one.

### The Firebase Realtime Database rules (`web/firebase/database.rules.json`)

The rules protect the database from anyone who signs in with the Firebase SDK directly, such as the mobile app. The web console's service account is not affected by them.

- **Everything is closed by default** (`.read` and `.write` are false at the root).
- **Field officers only** ("Standard User" with an Active or Invited account) get any access. Administrators use the web console.
- A field officer can **read**:
  - their own `users`, `user_locations` and `notifications`;
  - `locations`, `sensors`, `thresholds`, `alerts` and `alert_actions` of the provinces given to them;
  - `readings/{sensor}` only when `sensor_locations/{sensor}` points to one of their provinces.
- A field officer can **write** only:
  - their own `account_status` (Invited → Active, once), `last_active_at`, `notify_breach` and `notify_offline`;
  - an alert's `state`, forward only (Open → Acknowledged / Resolved, Acknowledged → Resolved);
  - a new `alert_actions` entry, for their own user ID, with a note that is not empty and at most 500 characters;
  - `is_read` and `read_at` of their own notifications.
- **Nobody** can write readings, sensors, thresholds, sites or users through the rules. Those come only from the gateway and the web console.
- `alerts/{location_id}` has an index on `state` and `raised_at`.

After any change to `database.rules.json`, paste the whole file into **Firebase console > Realtime Database > Rules** and click **Publish**.
