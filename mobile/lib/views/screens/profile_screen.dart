// SUMMARY: Screen M9, Profile (opened from the avatar on Home): account details, the assigned provinces, the three notification
// switches (threshold breaches, sensor offline, daily summary at 07:00), and Sign out.
// An Administrator who signs in on the phone gets the same Standard User functions as everyone else.
// MVVM: View. ProfileViewModel saves the switches and signs out; this file only draws.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/format.dart';
import '../../core/theme.dart';
import '../../viewmodels/profile_view_model.dart';
import '../../viewmodels/session_view_model.dart';
import '../../viewmodels/view_model_factory.dart';
import '../widgets/widgets.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  static Future<void> _toggle(BuildContext context, ProfileViewModel vm, {bool? breach, bool? offline, bool? daily}) async {
    final error = await vm.setNotification(breach: breach, offline: offline, daily: daily);
    if (error != null && context.mounted) showToast(context, 'Could not save', detail: error, error: true);
  }

  static Future<void> _signOut(BuildContext context, ProfileViewModel vm) async {
    final navigator = Navigator.of(context);
    navigator.popUntil((r) => r.isFirst); // close the profile first ...
    await vm.signOut(); // ... then the app goes back to the sign-in screen
  }

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
        create: (c) => c.read<ViewModelFactory>().profile()..load(),
        child: Consumer2<ProfileViewModel, SessionViewModel>(
          builder: (context, vm, session, _) {
            // While the page is closing after Sign out there is no person any more: draw nothing.
            if (session.user == null) return const Scaffold();
            final user = session.user!;
            return Scaffold(
      body: SafeArea(
        child: ListView(padding: const EdgeInsets.fromLTRB(20, 6, 20, 32), children: [
          Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
            RoundIconButton(Icons.chevron_left_rounded, onTap: () => Navigator.of(context).pop()),
            Text('Profile', style: sgText(16, weight: FontWeight.w700)),
            const SizedBox(width: 44),
          ]),
          const SizedBox(height: 20),
          Column(children: [
            Avatar(initials(user.name), size: 84, radius: 26, fontSize: 28),
            const SizedBox(height: 14),
            Text(user.name, style: sgText(20, weight: FontWeight.w700, spacing: -0.4)),
            const SizedBox(height: 3),
            Text(user.email, style: sgText(13, color: Sg.tx2)),
            const SizedBox(height: 10),
            // An Administrator on the phone still gets Standard User functions, so the label says so.
            StatusPill(vm.roleLabel, Tone.info),
          ]),
          const GroupLabel('Assigned provinces'),
          SgCard(
            padding: const EdgeInsets.all(15),
            child: ViewModelBody<Map<String, String>>(
              viewModel: vm,
              builder: (context, names) => Wrap(spacing: 8, runSpacing: 8, children: [
              for (final id in user.locationIds)
                Container(
                  height: 32,
                  padding: const EdgeInsets.symmetric(horizontal: 10),
                  decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(11), border: Border.all(color: Sg.line)),
                  child: Row(mainAxisSize: MainAxisSize.min, children: [
                    Container(width: 8, height: 8, decoration: BoxDecoration(color: Sg.locationColour(id), shape: BoxShape.circle)),
                    const SizedBox(width: 6),
                    Text(names[id] ?? id, style: sgText(13)),
                  ]),
                ),
              ]),
            ),
          ),
          const GroupLabel('Notifications'),
          SgCard(
            padding: EdgeInsets.zero,
            child: Column(children: [
              _switch(Icons.warning_amber_rounded, Sg.tCrit, Sg.outOfRange, 'Threshold breaches', user.notifyBreach, (v) => _toggle(context, vm, breach: v)),
              const Divider(height: 1, indent: 64, color: Sg.line2),
              _switch(Icons.wifi_off_rounded, Sg.tOff, Sg.offline, 'Sensor offline', user.notifyOffline, (v) => _toggle(context, vm, offline: v)),
              const Divider(height: 1, indent: 64, color: Sg.line2),
              _switch(Icons.schedule_rounded, Sg.tOk, Sg.inRange, 'Daily summary at 07:00', user.notifyDaily, (v) => _toggle(context, vm, daily: v)),
            ]),
          ),
          const SizedBox(height: 14),
          SgCard(
            padding: const EdgeInsets.symmetric(horizontal: 15, vertical: 13),
            onTap: () => _signOut(context, vm),
            child: Row(children: [
              const IconTile(Icons.logout_rounded, fg: Sg.outOfRange, bg: Sg.tCrit, size: 36, iconSize: 18, radius: 11),
              const SizedBox(width: 13),
              Text('Sign out', style: sgText(15, weight: FontWeight.w600, color: Sg.outOfRange)),
            ]),
          ),
          const SizedBox(height: 14),
          Center(child: Text('SensorGrid 1.0', style: sgText(12, color: Sg.tx4))),
        ]),
      ),
    );
          },
        ),
      );

  static Widget _switch(IconData icon, Color bg, Color fg, String label, bool on, ValueChanged<bool> onChanged) => Padding(
        padding: const EdgeInsets.symmetric(horizontal: 15, vertical: 12),
        child: Row(children: [
          IconTile(icon, fg: fg, bg: bg, size: 36, iconSize: 18, radius: 11),
          const SizedBox(width: 13),
          Expanded(child: Text(label, style: sgText(15, weight: FontWeight.w600))),
          GestureDetector(
            onTap: () => onChanged(!on),
            child: AnimatedContainer(
              duration: const Duration(milliseconds: 160),
              width: 46,
              height: 28,
              padding: const EdgeInsets.all(3),
              alignment: on ? Alignment.centerRight : Alignment.centerLeft,
              decoration: BoxDecoration(color: on ? Sg.brand : const Color(0xFFDDE5E1), borderRadius: BorderRadius.circular(14)),
              child: Container(width: 22, height: 22, decoration: const BoxDecoration(color: Colors.white, shape: BoxShape.circle, boxShadow: [BoxShadow(color: Color(0x330D2620), blurRadius: 4, offset: Offset(0, 2))])),
            ),
          ),
        ]),
      );
}
