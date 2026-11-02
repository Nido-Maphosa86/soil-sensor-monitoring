// SUMMARY: The named routes of the app (the pages opened on top of the tabs) and what each one needs to open.
// MVVM: View. main.dart hands AppRoutes.generate to MaterialApp, and screens open pages with Navigator.pushNamed(...).
// The four tabs (Home, Sites, Alerts, Reports) are not routes: they live inside the main frame.
import 'package:flutter/material.dart';

import '../models/entities.dart';
import '../models/enums.dart';
import 'screens/alert_detail_screen.dart';
import 'screens/location_detail_screen.dart';
import 'screens/notifications_screen.dart';
import 'screens/profile_screen.dart';
import 'screens/trend_screen.dart';

/// What the trend page needs: the province, the parameter, and the sensor to start on (null: the first sensor of the province).
class TrendArgs {
  const TrendArgs(this.locationId, this.parameter, [this.sensorId]);
  final String locationId;
  final Parameter parameter;
  final String? sensorId;
}

class AppRoutes {
  static const profile = '/profile'; // M9, no arguments
  static const location = '/location'; // M4, argument: the location id (String)
  static const trend = '/trend'; // M5, argument: TrendArgs
  static const alert = '/alert'; // M7, argument: the AlertItem that was tapped
  static const notifications = '/notifications'; // M10, no arguments

  /// Builds the page for a route name. Unknown names return null (Flutter then shows its own error).
  static Route<dynamic>? generate(RouteSettings settings) {
    final Widget? page = switch (settings.name) {
      AppRoutes.profile => const ProfileScreen(),
      AppRoutes.location => LocationDetailScreen(locationId: settings.arguments as String),
      AppRoutes.trend => TrendScreen(args: settings.arguments as TrendArgs),
      AppRoutes.alert => AlertDetailScreen(alert: settings.arguments as AlertItem),
      AppRoutes.notifications => const NotificationsScreen(),
      _ => null,
    };
    return page == null ? null : MaterialPageRoute(builder: (_) => page, settings: settings);
  }
}
