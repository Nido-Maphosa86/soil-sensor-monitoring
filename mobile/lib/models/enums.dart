// SUMMARY: The fixed lists of choices used across the app: roles, alert types, states, the seven measured parameters and so on.
// MVVM: Model. It knows nothing about screens or Firebase. Each value has the exact text that is stored in the database.

// The two kinds of account. Only Standard Users (field officers) may use the mobile app.
enum UserRole {
  administrator('Administrator'),
  standardUser('Standard User');

  const UserRole(this.stored); // each value carries the text saved in the database

  /// The text stored in `users/{uid}/role`.
  final String stored;
  // turns the database text back into a value; unknown text counts as a Standard User
  static UserRole fromStored(String s) => values.firstWhere((r) => r.stored == s, orElse: () => standardUser);
}

// Where an account stands: Invited until the first sign-in, then Active; Deactivated means locked out.
enum AccountStatus {
  active('Active'),
  invited('Invited'),
  deactivated('Deactivated');

  const AccountStatus(this.stored);
  final String stored;
  // unknown text counts as Deactivated, the safe choice
  static AccountStatus fromStored(String s) => values.firstWhere((r) => r.stored == s, orElse: () => deactivated);
}

// Why an alert was raised: a value outside its band, a silent sensor, or a low battery.
enum AlertType {
  breach('Breach'),
  offline('Offline'),
  battery('Battery');

  const AlertType(this.stored);
  final String stored;
  static AlertType fromStored(String s) => values.firstWhere((r) => r.stored == s, orElse: () => breach);
}

// How serious an alert is. Kept separate from the state (below).
enum AlertSeverity {
  critical('Critical'),
  warning('Warning');

  const AlertSeverity(this.stored);
  final String stored;
  static AlertSeverity fromStored(String s) => values.firstWhere((r) => r.stored == s, orElse: () => warning);
}

/// Alert state only moves forward: Open, then Acknowledged, then Resolved (or Open straight to Resolved).
enum AlertState {
  open('Open'),
  acknowledged('Acknowledged'),
  resolved('Resolved');

  const AlertState(this.stored);
  final String stored;
  static AlertState fromStored(String s) => values.firstWhere((r) => r.stored == s, orElse: () => open);
}

// The file type of a report made on the web console.
enum ReportFormat {
  pdf('PDF'),
  csv('CSV');

  const ReportFormat(this.stored);
  final String stored;
  static ReportFormat fromStored(String s) => values.firstWhere((r) => r.stored == s, orElse: () => pdf);
}

/// The seven stored values of a reading. Humidity is NOT stored.
enum Parameter {
  // database key, label on screen, unit, decimals shown
  soilMoisture('soil_moisture', 'Soil moisture', '%', 1),
  soilTemperature('soil_temperature', 'Temperature', '°C', 1),
  conductivity('conductivity', 'Conductivity', 'µS/cm', 0),
  ph('ph', 'pH level', 'pH', 1),
  nitrogen('nitrogen', 'Nitrogen (N)', 'mg/kg', 0),
  phosphorus('phosphorus', 'Phosphorus (P)', 'mg/kg', 0),
  potassium('potassium', 'Potassium (K)', 'mg/kg', 0);

  const Parameter(this.key, this.label, this.unit, this.decimals);

  /// The key used in the database.
  final String key;
  final String label;
  final String unit;
  final int decimals;

  /// A short name for alert titles, for example "Nitrogen".
  String get shortLabel => switch (this) {
        Parameter.soilMoisture => 'Soil moisture',
        Parameter.soilTemperature => 'Temperature',
        Parameter.conductivity => 'Conductivity',
        Parameter.ph => 'pH',
        Parameter.nitrogen => 'Nitrogen',
        Parameter.phosphorus => 'Phosphorus',
        Parameter.potassium => 'Potassium',
      };

  // finds the parameter for a database key, or null when the key is missing or unknown
  static Parameter? fromKey(String? k) => k == null ? null : values.cast<Parameter?>().firstWhere((p) => p!.key == k, orElse: () => null);
}

/// The traffic light shown for a value against its band.
enum BandStatus { inRange, nearLimit, outOfRange }

/// How far back a trend chart looks.
enum TrendRange {
  hours24('24 hours', Duration(hours: 24)),
  days7('7 days', Duration(days: 7)),
  days30('30 days', Duration(days: 30));

  const TrendRange(this.label, this.span);
  final String label; // the text on the chart's range buttons
  final Duration span; // how much time the chart covers
}