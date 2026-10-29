// SUMMARY: The ViewModel of M1 (Sign in), the forgot-password request, M1b ("Check your email"), "Access denied"
// and "Use the web console" (this app is for field officers only).
// MVVM: ViewModel. It holds what the sign-in screen shows (which part is open, busy, the error message) and does the work:
//   - a wrong email or password gets ONE message that never says which part was wrong;
//   - a deactivated account is signed out at once and shown "Access denied";
//   - an account that is not a field officer (Standard User) is signed out and told to use the web console;
//   - a person still signed in from last time is let straight in, after the same checks;
//   - the forgot-password answer is the same whether or not the account exists.
// The text boxes themselves stay in the View; it passes their text in.
import 'package:flutter/foundation.dart';

import '../models/entities.dart';
import '../models/enums.dart';
import '../repositories/repositories.dart';
import '../repositories/repository_set.dart';
import 'session_view_model.dart';

/// Which part of the sign-in screen is showing.
enum SignInMode { checking, signIn, forgot, sent, denied, webOnly }

class SignInViewModel extends ChangeNotifier {
  SignInViewModel(this._repositories, this._session);

  final RepositorySet _repositories;
  final SessionViewModel _session;

  late SignInMode _mode = _session.restoreTried ? SignInMode.signIn : SignInMode.checking;
  bool _busy = false;
  bool _showPassword = false;
  bool _keepSignedIn = true;
  String? _error;
  bool _disposed = false;

  SignInMode get mode => _mode;
  bool get busy => _busy;
  bool get showPassword => _showPassword;
  bool get keepSignedIn => _keepSignedIn;
  String? get error => _error;

  void togglePassword() {
    _showPassword = !_showPassword;
    _notify();
  }

  void toggleKeepSignedIn() {
    _keepSignedIn = !_keepSignedIn;
    _notify();
  }

  /// Opens another part of the screen (for example "Forgot password?" or "Back to sign in").
  void showMode(SignInMode mode) {
    _mode = mode;
    _error = null;
    _notify();
  }

  /// Hides the error message.
  void clearError() {
    if (_error == null) return;
    _error = null;
    _notify();
  }

  /// At start: lets in the person still signed in from last time (Firebase keeps the sign-in on the phone), once per app start.
  Future<void> restore() async {
    if (_session.restoreTried) return;
    _session.restoreTried = true;
    try {
      final user = await _repositories.auth.currentUser();
      if (user != null) {
        _session.signedIn(await _check(user));
        return;
      }
    } on Exception {
      // Not signed in, deactivated, not a field officer or offline: the sign-in screen explains on the next attempt.
    }
    _mode = SignInMode.signIn;
    _notify();
  }

  /// Signs in. On success the SessionViewModel gets the person, and the app shows the main tabs.
  Future<void> signIn(String email, String password) async {
    _busy = true;
    _error = null;
    _notify();
    try {
      if (email.trim().isEmpty || password.isEmpty) throw const SignInFailed();
      _session.signedIn(await _check(await _repositories.auth.signIn(email.trim(), password)));
    } on AccessDenied {
      _mode = SignInMode.denied;
    } on WebConsoleOnly {
      _mode = SignInMode.webOnly;
    } on SignInFailed {
      _error = 'The email or password is not correct.';
    } on BackendUnavailable catch (e) {
      _error = e.message;
    } finally {
      _busy = false;
      _notify();
    }
  }

  /// The rules every signed-in person must pass. A refused person is signed out again.
  Future<AppUser> _check(AppUser user) async {
    if (user.status == AccountStatus.deactivated) {
      await _repositories.auth.signOut();
      throw const AccessDenied();
    }
    if (user.role != UserRole.standardUser) {
      await _repositories.auth.signOut();
      throw const WebConsoleOnly();
    }
    // An invitation is finished by the first sign in (the repository sets the status to Active), so anything else is refused.
    if (user.status != AccountStatus.active) {
      await _repositories.auth.signOut();
      throw const SignInFailed();
    }
    return user;
  }

  /// Sends the reset link. The screen shows "Check your email" whatever happens, so nothing reveals which emails have accounts.
  Future<void> sendReset(String email) async {
    _busy = true;
    _notify();
    try {
      if (email.trim().isNotEmpty) await _repositories.auth.sendPasswordReset(email.trim());
    } catch (_) {
      // Deliberately ignored: the answer must not depend on whether the account exists.
    }
    _busy = false;
    _mode = SignInMode.sent;
    _notify();
  }

  /// Tells the View to redraw, unless the screen has already closed (it closes as soon as sign-in succeeds).
  void _notify() {
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }
}
