// SUMMARY: Screens M1 (Sign in), the forgot-password request, M1b ("Check your email"), the "Access denied" state and the
// "web console only" state for administrators (this app is for field officers only).
// A wrong email or password gets ONE message that does not say which part was wrong. The forgot-password answer is the same
// whether or not the account exists. A deactivated account is signed out and shown "Access denied".
// MVVM: View. SignInViewModel decides what happens and which part is shown; this file keeps only the two text boxes and draws.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/theme.dart';
import '../../viewmodels/sign_in_view_model.dart';
import '../../viewmodels/view_model_factory.dart';
import '../widgets/widgets.dart';

/// Creates the SignInViewModel and shows the sign-in page.
class SignInScreen extends StatelessWidget {
  const SignInScreen({super.key});

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
        create: (c) => c.read<ViewModelFactory>().signIn()..restore(),
        child: const _SignInPage(),
      );
}

class _SignInPage extends StatefulWidget {
  const _SignInPage();

  @override
  State<_SignInPage> createState() => _SignInPageState();
}

class _SignInPageState extends State<_SignInPage> {
  // The text boxes belong to the View; the ViewModel gets their text when a button is pressed.
  final _email = TextEditingController();
  final _password = TextEditingController();

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final vm = context.watch<SignInViewModel>();
    return Scaffold(
      backgroundColor: Sg.deep,
      body: LayoutBuilder(
        builder: (context, c) => SingleChildScrollView(
          child: ConstrainedBox(
            constraints: BoxConstraints(minHeight: c.maxHeight),
            child: IntrinsicHeight(
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                const SignInHero(),
                Expanded(
                  child: Container(
                    width: double.infinity,
                    decoration: const BoxDecoration(color: Sg.canvas, borderRadius: BorderRadius.vertical(top: Radius.circular(32))),
                    padding: const EdgeInsets.fromLTRB(24, 28, 24, 24),
                    child: switch (vm.mode) {
                      SignInMode.checking => const Padding(
                          padding: EdgeInsets.all(48),
                          child: Center(child: CircularProgressIndicator(color: Sg.brand)),
                        ),
                      SignInMode.signIn => _signInForm(vm),
                      SignInMode.forgot => _forgotForm(vm),
                      SignInMode.sent => _sent(vm),
                      SignInMode.denied => _denied(vm),
                      SignInMode.webOnly => _webOnly(vm),
                    },
                  ),
                ),
              ]),
            ),
          ),
        ),
      ),
    );
  }

  Widget _field(SignInViewModel vm, String label, TextEditingController controller, IconData icon, {bool password = false, bool autofocus = false, TextInputType? type}) {
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text(label.toUpperCase(), style: sgText(10, weight: FontWeight.w600, color: Sg.tx2, spacing: 1)),
      const SizedBox(height: 8),
      Container(
        height: 52,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(14), border: Border.all(color: Sg.line)),
        child: Row(children: [
          Icon(icon, size: 19, color: Sg.tx3),
          const SizedBox(width: 12),
          Expanded(
            child: TextField(
              controller: controller,
              autofocus: autofocus,
              obscureText: password && !vm.showPassword,
              keyboardType: type,
              autocorrect: false,
              enableSuggestions: false,
              autofillHints: password ? const [AutofillHints.password] : const [AutofillHints.username],
              style: sgText(15),
              decoration: const InputDecoration(border: InputBorder.none, isDense: true),
              onSubmitted: (_) => password ? vm.signIn(_email.text, _password.text) : null,
            ),
          ),
          if (password)
            GestureDetector(
              onTap: vm.togglePassword,
              child: Icon(vm.showPassword ? Icons.visibility_off_outlined : Icons.visibility_outlined, size: 19, color: Sg.tx3),
            ),
        ]),
      ),
    ]);
  }

  Widget _signInForm(SignInViewModel vm) => Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text('Sign in to the console', style: sgText(20, weight: FontWeight.w700, spacing: -0.4)),
        const SizedBox(height: 4),
        Text('Use the credentials issued by the Department of Agriculture.', style: sgText(13, color: Sg.tx2)),
        const SizedBox(height: 20),
        _field(vm, 'Email address', _email, Icons.mail_outline_rounded, type: TextInputType.emailAddress),
        const SizedBox(height: 14),
        _field(vm, 'Password', _password, Icons.lock_outline_rounded, password: true),
        const SizedBox(height: 14),
        Row(children: [
          GestureDetector(
            onTap: vm.toggleKeepSignedIn,
            child: Row(children: [
              Container(
                width: 18,
                height: 18,
                decoration: BoxDecoration(color: vm.keepSignedIn ? Sg.brand : Colors.white, borderRadius: BorderRadius.circular(5), border: Border.all(color: vm.keepSignedIn ? Sg.brand : Sg.line)),
                child: vm.keepSignedIn ? const Icon(Icons.check_rounded, size: 13, color: Colors.white) : null,
              ),
              const SizedBox(width: 9),
              Text('Keep me signed in', style: sgText(13, color: Sg.tx2)),
            ]),
          ),
          const Spacer(),
          GestureDetector(
            onTap: () => vm.showMode(SignInMode.forgot),
            child: Text('Forgot password?', style: sgText(13, weight: FontWeight.w600, color: Sg.brand)),
          ),
        ]),
        if (vm.error != null) ...[
          const SizedBox(height: 14),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(color: Sg.tCrit, borderRadius: BorderRadius.circular(12)),
            child: Text(vm.error!, style: sgText(13, weight: FontWeight.w600, color: Sg.outOfRange)),
          ),
        ],
        const SizedBox(height: 18),
        SgButton('Sign in', loading: vm.busy, onPressed: () => vm.signIn(_email.text, _password.text)),
        const SizedBox(height: 16),
        Text('For field officers. You will see readings for the provinces assigned to you.',
            textAlign: TextAlign.center, style: sgText(12, color: Sg.tx3, height: 1.55)),
      ]);

  Widget _forgotForm(SignInViewModel vm) => Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text('Forgot your password?', style: sgText(20, weight: FontWeight.w700, spacing: -0.4)),
        const SizedBox(height: 4),
        Text('Enter the email address you sign in with and we will send a reset link.', style: sgText(13, color: Sg.tx2, height: 1.5)),
        const SizedBox(height: 20),
        _field(vm, 'Email address', _email, Icons.mail_outline_rounded, type: TextInputType.emailAddress, autofocus: true),
        const SizedBox(height: 20),
        SgButton('Send reset link', loading: vm.busy, onPressed: () => vm.sendReset(_email.text)),
        const SizedBox(height: 22),
        _backLink(vm),
      ]);

  /// M1b: the same message whether or not the email matched an account.
  Widget _sent(SignInViewModel vm) => Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        const SizedBox(height: 8),
        const IconTile(Icons.mail_outline_rounded, fg: Sg.brand, bg: Sg.tOk, size: 56, iconSize: 26, radius: 16),
        const SizedBox(height: 22),
        Text('Check your email', style: sgText(28, weight: FontWeight.w700, spacing: -0.5)),
        const SizedBox(height: 12),
        Text(
            'If that address matches an account, a password reset link is on its way. It can take a few minutes to arrive. Check your spam folder if it does not show up.',
            style: sgText(15, color: Sg.tx2, height: 1.65)),
        const SizedBox(height: 30),
        _backLink(vm),
      ]);

  Widget _denied(SignInViewModel vm) => Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        const SizedBox(height: 8),
        const IconTile(Icons.block_rounded, fg: Sg.outOfRange, bg: Sg.tCrit, size: 56, iconSize: 26, radius: 16),
        const SizedBox(height: 22),
        Text('Access denied', style: sgText(28, weight: FontWeight.w700, spacing: -0.5)),
        const SizedBox(height: 12),
        Text('This account has been deactivated. Please contact your administrator if you think this is a mistake.', style: sgText(15, color: Sg.tx2, height: 1.65)),
        const SizedBox(height: 30),
        _backLink(vm),
      ]);

  /// An administrator (or any account that is not a field officer) signed in: this app is for field officers only.
  Widget _webOnly(SignInViewModel vm) => Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        const SizedBox(height: 8),
        const IconTile(Icons.desktop_windows_outlined, fg: Sg.info, bg: Sg.tInfo, size: 56, iconSize: 26, radius: 16),
        const SizedBox(height: 22),
        Text('Use the web console', style: sgText(28, weight: FontWeight.w700, spacing: -0.5)),
        const SizedBox(height: 12),
        Text('The SensorGrid app is for field officers. Administrators sign in on the SensorGrid web console instead.',
            style: sgText(15, color: Sg.tx2, height: 1.65)),
        const SizedBox(height: 30),
        _backLink(vm),
      ]);

  Widget _backLink(SignInViewModel vm) => GestureDetector(
        onTap: () {
          _password.clear();
          vm.showMode(SignInMode.signIn);
        },
        child: Row(children: [
          const Icon(Icons.chevron_left_rounded, size: 22, color: Sg.brand),
          const SizedBox(width: 4),
          Text('Back to sign in', style: sgText(15, weight: FontWeight.w600, color: Sg.brand)),
        ]),
      );
}

/// The dark brand panel at the top of the sign-in screen (also used on the "Waiting for the Firebase connection" screen).
class SignInHero extends StatelessWidget {
  const SignInHero({super.key});

  @override
  Widget build(BuildContext context) => Container(
        color: Sg.deep,
        child: Stack(children: [
          Positioned(
            left: -120,
            top: -260,
            child: Container(width: 440, height: 440, decoration: const BoxDecoration(shape: BoxShape.circle, gradient: RadialGradient(colors: [Color(0xB310795A), Color(0x0010795A)]))),
          ),
          SafeArea(
            bottom: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(24, 22, 24, 0),
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Row(children: [
                  const SensorGridMark(size: 40),
                  const SizedBox(width: 14),
                  Text('SensorGrid', style: sgText(16, weight: FontWeight.w700, color: Colors.white, spacing: -0.16)),
                ]),
                const SizedBox(height: 28),
                Text('Three provinces.\nFifteen sensors.', style: sgText(32, weight: FontWeight.w700, color: Colors.white, spacing: -0.96, height: 1.14)),
                const SizedBox(height: 12),
                Text('Soil readings across the Free State, Gauteng and North West, every thirty minutes.',
                    style: sgText(13, color: const Color(0xFF9FB5AC), height: 1.55)),
                const SizedBox(height: 18),
                const SizedBox(height: 96, width: double.infinity, child: CustomPaint(painter: _FieldPainter())),
              ]),
            ),
          ),
        ]),
      );
}

/// The SensorGrid logo mark: a probe housing, three prongs and a row of grid nodes.
class SensorGridMark extends StatelessWidget {
  const SensorGridMark({super.key, this.size = 40});
  final double size;

  @override
  Widget build(BuildContext context) => Container(
        width: size,
        height: size,
        decoration: BoxDecoration(color: Sg.mint, borderRadius: BorderRadius.circular(size * 0.3)),
        child: CustomPaint(painter: _MarkPainter()),
      );
}

class _MarkPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final s = size.width / 96;
    final dark = Paint()..color = Sg.deep;
    canvas.drawRRect(RRect.fromRectAndRadius(Rect.fromLTWH(41 * s, 18 * s, 14 * s, 20 * s), Radius.circular(6 * s)), dark);
    canvas.drawRRect(RRect.fromRectAndRadius(Rect.fromLTWH(33 * s, 41 * s, 30 * s, 5 * s), Radius.circular(2.5 * s)), dark);
    final prong = Paint()
      ..color = Sg.deep
      ..strokeWidth = 3 * s
      ..strokeCap = StrokeCap.round;
    for (final x in [38.0, 48.0, 58.0]) {
      canvas.drawLine(Offset(x * s, 48 * s), Offset(x * s, 60 * s), prong);
    }
    canvas.drawCircle(Offset(38 * s, 72 * s), 4 * s, Paint()..color = const Color(0x730D2620));
    canvas.drawCircle(Offset(48 * s, 72 * s), 5 * s, dark);
    canvas.drawCircle(Offset(58 * s, 72 * s), 4 * s, Paint()..color = const Color(0x730D2620));
  }

  @override
  bool shouldRepaint(covariant CustomPainter old) => false;
}

/// A small drawing: soil layers, a pigeon pea plant and a sensor with a reading label.
class _FieldPainter extends CustomPainter {
  const _FieldPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width, h = size.height;
    canvas.drawRect(Rect.fromLTWH(0, h * 0.57, w, h * 0.19), Paint()..color = const Color(0x2161C9A0));
    canvas.drawRect(Rect.fromLTWH(0, h * 0.76, w, h * 0.15), Paint()..color = const Color(0x1761C9A0));
    canvas.drawRect(Rect.fromLTWH(0, h * 0.91, w, h * 0.09), Paint()..color = const Color(0x0D61C9A0));
    canvas.drawLine(Offset(0, h * 0.57), Offset(w, h * 0.57), Paint()..color = const Color(0x8061C9A0)..strokeWidth = 1.5);

    // plant
    final px = w * 0.16;
    final stem = Paint()..color = const Color(0xFF4FB68E)..strokeWidth = 2.5..strokeCap = StrokeCap.round;
    canvas.drawLine(Offset(px, h * 0.57), Offset(px, h * 0.15), stem);
    final leaf = Paint()..color = Sg.mint;
    void leafAt(double dx, double dy, double rw, double rh, double angle) {
      canvas.save();
      canvas.translate(px + dx, h * dy);
      canvas.rotate(angle);
      canvas.drawOval(Rect.fromCenter(center: Offset.zero, width: rw, height: rh), leaf);
      canvas.restore();
    }
    leafAt(-16, 0.40, 30, 11, -0.35);
    leafAt(16, 0.40, 30, 11, 0.35);
    leafAt(-12, 0.26, 22, 9, -0.45);
    leafAt(12, 0.26, 22, 9, 0.45);
    leafAt(0, 0.12, 9, 18, 0);

    // sensor
    final sx = w * 0.64;
    final body = Paint()..color = Sg.mint;
    canvas.drawRRect(RRect.fromRectAndRadius(Rect.fromLTWH(sx - 10, h * 0.22, 20, 18), const Radius.circular(7)), body);
    canvas.drawRRect(RRect.fromRectAndRadius(Rect.fromLTWH(sx - 14, h * 0.40, 28, 5), const Radius.circular(2.5)), body);
    final prongs = Paint()..color = Sg.mint..strokeWidth = 2.5..strokeCap = StrokeCap.round;
    for (final dx in [-7.0, 0.0, 7.0]) {
      canvas.drawLine(Offset(sx + dx, h * 0.46), Offset(sx + dx, h * (dx == 0 ? 0.98 : 0.86)), prongs);
    }
    final wave = Paint()..style = PaintingStyle.stroke..strokeWidth = 1.8..strokeCap = StrokeCap.round;
    for (final r in [14.0, 23.0]) {
      wave.color = Sg.mint.withValues(alpha: r == 14 ? 0.55 : 0.28);
      canvas.drawArc(Rect.fromCircle(center: Offset(sx, h * 0.22), radius: r), 3.4, 2.6, false, wave);
    }

    // reading label
    final rect = RRect.fromRectAndRadius(Rect.fromLTWH(w * 0.76, h * 0.18, w * 0.24, h * 0.37), const Radius.circular(11));
    canvas.drawRRect(rect, Paint()..color = const Color(0x14FFFFFF));
    canvas.drawRRect(rect, Paint()..style = PaintingStyle.stroke..color = const Color(0x4D61C9A0));
    void label(String text, double dy, Color c, double size, bool bold) {
      final tp = TextPainter(text: TextSpan(text: text, style: sgText(size, weight: bold ? FontWeight.w600 : FontWeight.w500, color: c)), textDirection: TextDirection.ltr)..layout();
      tp.paint(canvas, Offset(w * 0.76 + 11, h * dy));
    }
    label('Moisture', 0.23, const Color(0xFF9FB5AC), 10, false);
    label('24.6 %', 0.36, Colors.white, 12, true);
  }

  @override
  bool shouldRepaint(covariant CustomPainter old) => false;
}
