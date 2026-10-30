// SUMMARY: The main frame after sign in: four tabs (Home, Sites, Alerts, Reports) with the floating tab bar.
// The Alerts tab shows the number of open alerts. The avatar on Home opens the profile (M9).
// MVVM: View. The open tab and the badge number come from ShellViewModel; this file only draws them.
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/theme.dart';
import '../../viewmodels/shell_view_model.dart';
import '../../viewmodels/view_model_factory.dart';
import '../widgets/widgets.dart';
import 'alerts_screen.dart';
import 'home_screen.dart';
import 'reports_screen.dart';
import 'sites_screen.dart';

/// The padding that keeps content clear of the floating tab bar.
const double tabBarClearance = 120;

/// A scrolling page used by the four tabs.
class TabPage extends StatelessWidget {
  const TabPage({super.key, required this.children});
  final List<Widget> children;

  @override
  Widget build(BuildContext context) => SafeArea(
        bottom: false,
        child: ListView(padding: const EdgeInsets.fromLTRB(20, 8, 20, tabBarClearance), children: children),
      );
}

class MainShell extends StatelessWidget {
  const MainShell({super.key});

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
        create: (c) => c.read<ViewModelFactory>().shell()..loadBadge(),
        child: Consumer<ShellViewModel>(
          builder: (context, vm, _) => Scaffold(
            body: Stack(children: [
              // IndexedStack keeps all four tabs (and their ViewModels) alive while you switch between them.
              IndexedStack(index: vm.index, children: [
                HomeScreen(onSeeAll: () => vm.selectTab(1)),
                const SitesScreen(),
                const AlertsScreen(),
                const ReportsScreen(),
              ]),
              Positioned(
                left: 0,
                right: 0,
                bottom: 0,
                child: IgnorePointer(
                  child: Container(
                    height: 170,
                    decoration: const BoxDecoration(gradient: LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Color(0x00F3F6F4), Sg.canvas])),
                  ),
                ),
              ),
              Positioned(
                left: 16,
                right: 16,
                bottom: 0,
                child: SafeArea(
                  child: Padding(padding: const EdgeInsets.only(bottom: 14), child: FloatingTabBar(index: vm.index, onTap: vm.selectTab, openAlerts: vm.openAlerts)),
                ),
              ),
            ]),
          ),
        ),
      );
}
