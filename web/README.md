# SensorGrid web console

This folder holds the SensorGrid web console. It contains **no sample data**. Every site, sensor, reading, alert and account comes from
the Firebase project. Until the Firebase settings are filled in, every page shows **"Waiting for the Firebase connection"** and lists the missing settings.

| Folder | What is in it |
|---|---|
| `web/src/SensorGrid.Web/Domain` | Entities, enums and business rules (breach, 2 hour offline, 25% battery, note required, state only moves forward). |
| `web/src/SensorGrid.Web/Repository` | The Firebase connection: Realtime Database store, Firebase Authentication, access token, report files. Every repository. |
| `web/src/SensorGrid.Web/Service` | Use cases, province filtering, alert evaluator, CSV and PDF writers, first-administrator setup. |
| `web/src/SensorGrid.Web/Controllers`, `Views` | Pages W1 to W12, matching `docs/design/Web_Application_Design.html`. |
| `firebase/` | `database.rules.json` (security rules for the mobile app) and `firebase.json` (rules deploy and local emulators). |

Every place where the connection happens is marked `// CONNECT FIREBASE HERE`.

## Who can use what

| Role (stored value) | Shown as | Web console | Mobile app |
|---|---|---|---|
| `Administrator` | Administrator | Yes, everything | **No.** The database rules give administrators no access from the app. |
| `Standard User` | Field officer | Yes, assigned provinces only | Yes, assigned provinces only |

The same Firebase Authentication account signs in to both. Deactivating an account in **User accounts** disables it in Firebase,
so it stops working on the web and the mobile app at once.

## Connect Firebase

1. Create the Firebase project. In the console:
   - **Authentication > Sign-in method:** enable **Email/Password**.
   - **Build > Realtime Database:** create the database.
   - **Authentication > Settings > Authorized domains:** add the domain the web console runs on.
   - **Authentication > Templates > Password reset:** reword the email so it works for invitations too. For example: *"Use this link to set your SensorGrid password. Field officers can then sign in on the web console and the mobile app."*
2. Deploy the security rules (they also create the alerts index). Install the Firebase CLI first:
   ```bash
   npm install -g firebase-tools
   ```
   ```bash
   cd web/firebase
   ```
   ```bash
   firebase deploy --only database --project <your-project-id>
   ```
3. **Project settings > Service accounts > Generate new private key.** Save the file **outside** this repository, for example `C:\keys\sensorgrid-admin.json`.
4. Fill in the settings with user-secrets (run these inside `web/src/SensorGrid.Web`):
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
   dotnet user-secrets set "SensorGrid:WebAppUrl" "https://the-address-of-this-console"
   ```
   On a server, use environment variables instead, for example `SensorGrid__Firebase__ProjectId`.
5. **First administrator.** A new database has no accounts. Set these, then start the app once:
   ```bash
   dotnet user-secrets set "SensorGrid:FirstAdministrator:Email" "you@example.org"
   ```
   ```bash
   dotnet user-secrets set "SensorGrid:FirstAdministrator:Name" "Your Name"
   ```
   The app creates the account and Firebase emails a "set your password" link. This only happens while the database has no accounts.
6. Run the console:
   ```bash
   dotnet run --project web/src/SensorGrid.Web
   ```
   Sign in, then:
   - **Sites > Add site:** add each site. The code is the two-letter prefix the sensor gateway uses (FS for FS-01).
   - **Thresholds:** review each site's bands.
   - **Sensors > Register sensor:** register the sensors.
   - **User accounts > Invite user:** invite people.

## Inviting people

**Invite user** does four things:
1. Creates the Firebase Authentication account. Nobody knows its random password.
2. Writes `users/{uid}` with status **Invited**, plus `user_locations` and `location_users`.
3. Asks Firebase to email a "set your password" link.
4. Shows **Resend invitation** next to the account until the person signs in for the first time. The status then becomes **Active**, on the web or in the mobile app.

## Data that comes from elsewhere

- **Readings and sensor status:** written by the sensor gateway to `readings/{loc}/{sensor}/{yyyyMMddHHmm}` and `sensor_status/...` (UTC keys), with the field names of the Realtime Database design in the SRS. The console only reads them.
- **Alerts and notifications:** written by the alert evaluator. On the **Blaze** plan, run it as a scheduled Cloud Function. On the free **Spark** plan, set `SensorGrid:RunAlertEvaluatorInWebApp` to `true` and the console runs it every minute.
- **Report files:** saved in `App_Data/reports` on the web server. On the Blaze plan, set `SensorGrid:UseCloudStorageForReports` to `true` and `SensorGrid:Firebase:StorageBucket` to put them in Cloud Storage.

## The mobile app

The field-officer mobile app follows these rules:
- Sign in with Firebase Authentication (email and password).
- Read `users/{uid}`. If it cannot be read, or the role is not `Standard User`, sign out and say *"Administrators use the web console."* The rules refuse administrators anyway.
- If `account_status` is `Invited`, set it to `Active`.
- Read only the provinces in `user_locations/{uid}`.
- Acknowledge or resolve an alert in **one** multi-path update. Write `alert_actions/{loc}/{id}/{acknowledged|resolved}` (`user_id`, `note`, `acted_at`) and `alerts/{loc}/{id}/state` together. The rules require the note.

## Testing locally without a Firebase project

The Firebase Emulator Suite needs Java. Start the emulators:
```bash
cd web/firebase
```
```bash
firebase emulators:start --only auth,database
```
Then set these and run the console:
- `SensorGrid:Firebase:UseEmulators` to `true`
- `SensorGrid:Firebase:DatabaseUrl` to `http://127.0.0.1:9000/?ns=<project-id>`
- `SensorGrid:Firebase:ProjectId` and `WebApiKey` to any values

No key file is needed. The emulator shows the invitation emails in its log instead of sending them.
