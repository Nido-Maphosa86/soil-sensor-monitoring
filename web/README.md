# SensorGrid web console

## What the app does

SensorGrid monitors the soil on pigeon pea farms in different provinces (for example Free State, FS).
Sensors in the soil measure **moisture, temperature, pH, conductivity, nitrogen, phosphorus and potassium**.
A sensor gateway sends a new reading about every 6 seconds to a Firebase database, and this web console shows them.

With the web console you can:
- see a **dashboard** of every province, with a moisture chart for the last 24 hours, 7 days or 30 days;
- look at each **site** and its **sensors**, and see which sensors are online or offline;
- read the **readings log** and download it as a CSV file;
- set the **thresholds** (the allowed minimum and maximum of each value);
- get **alerts** when a value goes outside its threshold, or when a sensor stops sending for 2 hours, and acknowledge or resolve them with a note;
- create **reports** (PDF or CSV);
- manage **user accounts** (invite people and give them provinces).

There are two kinds of user:

| User | What they can do |
|---|---|
| **Administrator** | Everything, in the web console only. |
| **Field officer** | Sees only the provinces given to them, in the web console and the mobile app. |

The app has no sample data. Everything comes from the Firebase project.

## What you need

- The **.NET 10 SDK**. Check it with `dotnet --version`.
- The project's **Firebase settings** and its **service-account key file**. Ask the project owner for them. Never put the key file inside this repository.

## Run the app

1. Open a terminal in the web project folder:
   ```bash
   cd web/src/SensorGrid.Web
   ```
2. Fill in the Firebase settings. You only do this once on each computer. Use the values from the project owner:
   ```bash
   dotnet user-secrets set "SensorGrid:Firebase:ProjectId" "the-project-id"
   ```
   ```bash
   dotnet user-secrets set "SensorGrid:Firebase:DatabaseUrl" "the-database-url"
   ```
   ```bash
   dotnet user-secrets set "SensorGrid:Firebase:WebApiKey" "the-web-api-key"
   ```
   ```bash
   dotnet user-secrets set "SensorGrid:Firebase:ServiceAccountKeyPath" "C:\keys\sensorgrid-admin.json"
   ```
   ```bash
   dotnet user-secrets set "SensorGrid:WebAppUrl" "http://localhost:5300"
   ```
   ```bash
   dotnet user-secrets set "SensorGrid:RunAlertEvaluatorInWebApp" "true"
   ```
   Check them with:
   ```bash
   dotnet user-secrets list
   ```
3. Build the app. It must say **Build succeeded**:
   ```bash
   dotnet build
   ```
4. Start the app:
   ```bash
   dotnet run
   ```
5. Open **http://localhost:5300** in your browser and sign in.
6. To stop the app, press **Ctrl + C** in the terminal.

If the page says **"Waiting for the Firebase connection"**, a setting from step 2 is missing. The page lists which one.

## The first time (administrator only)

A new database has no accounts yet. Before the first start, set your own email and name:
```bash
dotnet user-secrets set "SensorGrid:FirstAdministrator:Email" "you@example.org"
```
```bash
dotnet user-secrets set "SensorGrid:FirstAdministrator:Name" "Your Name"
```
Start the app. Firebase emails you a link to set your password. Then sign in and:
1. **Sites > Add site:** add each site, for example **FS**.
2. **Sensors > Register sensor:** register each sensor with exactly the code the gateway uses, for example **FS-01** under FS.
3. **Thresholds:** check the minimum and maximum of each value.
4. **User accounts > Invite user:** invite the field officers and give them their provinces. Each person gets an email to set their password.

## Where the readings come from

The sensor gateway is built by a third party. It writes to the database like this:

```
readings / FS-01 / history / (a new row about every 6 seconds)
readings / FS-01 / latest  (the newest row)
```

The app reads these rows as they are and uses these fields:

| Gateway field | Shown as |
|---|---|
| `timestamp` | the time of the reading |
| `moisture` | soil moisture, divided by 10 (281 shows as 28.1 %) |
| `temperature` | soil temperature, °C |
| `ec` | conductivity, µS/cm |
| `ph`, `nitrogen`, `phosphorus`, `potassium` | the same names |

Good to know:
- The gateway sends **no location**. The app finds the site from the registered sensor (FS-01 belongs to FS), so the sensor codes must match.
- The gateway sends **no battery or signal**, so the app does not show them.
- A sensor is **Offline** when no new row has come for 2 hours.
- A row of zeros means the probe is not in the soil. It is shown, but it does not raise an alert.

## Folders

| Folder | What is in it |
|---|---|
| `web/src/SensorGrid.Web/Domain` | The business rules (breach, 2 hours offline, a note is required). |
| `web/src/SensorGrid.Web/Repository` | Reading and writing the Firebase database. |
| `web/src/SensorGrid.Web/Service` | What the app does: dashboards, alerts, reports, user accounts. |
| `web/src/SensorGrid.Web/Controllers`, `Views` | The pages you see in the browser. |
| `web/firebase` | The database security rules. Publish them in **Firebase > Realtime Database > Rules**. |

The app is built with **Onion Architecture**: the pages only talk to the services, the services talk to the repositories, and only the repositories talk to Firebase.

## Something is wrong?

| Problem | What to do |
|---|---|
| "Waiting for the Firebase connection" | A setting is missing. Run `dotnet user-secrets list` and compare it with step 2. |
| "The file it points to does not exist" | The key file path is wrong. Check `ServiceAccountKeyPath`. |
| A sensor shows **Offline** | No new row in 2 hours. Check the gateway is running, and that the sensor code matches exactly. |
| No readings at all | Check the sensor is registered with the same code the gateway uses (FS-01). |
| Port 5300 is already in use | Another copy of the app is running. Stop it with **Ctrl + C**. |