// SUMMARY: Makes a new ViewModel for each screen, already connected to the repositories and the session.
// MVVM: ViewModel. main.dart creates ONE factory and shares it with Provider. A View asks it for its ViewModel
// (for example context.read<ViewModelFactory>().home()), so no View ever imports or touches a repository.
import '../models/enums.dart';
import '../repositories/repository_set.dart';
import 'alert_detail_view_model.dart';
import 'alerts_view_model.dart';
import 'home_view_model.dart';
import 'location_detail_view_model.dart';
import 'profile_view_model.dart';
import 'reports_view_model.dart';
import 'session_view_model.dart';
import 'shell_view_model.dart';
import 'sign_in_view_model.dart';
import 'sites_view_model.dart';
import 'trend_view_model.dart';

class ViewModelFactory {
  ViewModelFactory(this._repositories, this._session);

  final RepositorySet _repositories;
  final SessionViewModel _session;

  SignInViewModel signIn() => SignInViewModel(_repositories, _session);
  ShellViewModel shell() => ShellViewModel(_repositories, _session);
  HomeViewModel home() => HomeViewModel(_repositories, _session);
  SitesViewModel sites() => SitesViewModel(_repositories, _session);
  LocationDetailViewModel locationDetail(String locationId) => LocationDetailViewModel(_repositories, _session, locationId);
  TrendViewModel trend(String locationId, Parameter parameter) => TrendViewModel(_repositories, _session, locationId: locationId, parameter: parameter);
  AlertsViewModel alerts() => AlertsViewModel(_repositories, _session);
  AlertDetailViewModel alertDetail(String locationId, String alertId) => AlertDetailViewModel(_repositories, _session, locationId: locationId, alertId: alertId);
  ReportsViewModel reports() => ReportsViewModel(_repositories, _session);
  ProfileViewModel profile() => ProfileViewModel(_repositories, _session);
}
