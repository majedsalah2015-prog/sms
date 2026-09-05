import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../l10n/strings.dart';
import '../models/me.dart';
import '../models/portal.dart';
import '../state/auth_controller.dart';
import 'format.dart';
import 'theme.dart';
import 'widgets/async_view.dart';
import 'widgets/panels.dart';

/// The whole family's position in one figure, with the per-student breakdown
/// behind it.
///
/// The total is the server's — `IStatementService` is the single central
/// computation BR-FEE-008 requires. This screen does not add the students up,
/// because a phone that did its own arithmetic is how a family and the accounts
/// office start disagreeing about what is owed.
class StatementPage extends StatelessWidget {
  const StatementPage({super.key});

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final AuthController auth = context.watch<AuthController>();
    final Me? me = auth.me;

    final Map<int, String> names = <int, String>{
      for (final MeChild c in me?.children ?? const <MeChild>[])
        c.studentId: s.pair(c.nameEn, c.nameAr),
    };

    return Scaffold(
      appBar: AppBar(
        title: Row(
          children: <Widget>[
            Icon(Section.fees.icon, size: 20, color: Section.fees.color),
            const SizedBox(width: 8),
            Text(s.statement),
          ],
        ),
      ),
      body: AsyncView<PortalStatement>(
        load: () => auth.api.statement(),
        builder: (BuildContext context, PortalStatement statement) {
          final bool owing = statement.total > 0;
          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
            children: <Widget>[
              Panel(
                children: <Widget>[
                  BigStat(
                    section: Section.fees,
                    label: s.familyTotal,
                    value: owing
                        ? Fmt.money(statement.total, statement.currency, s.lang)
                        : s.settled,
                  ),
                ],
              ),
              const SizedBox(height: 12),
              for (final PortalFees fees in statement.students) ...<Widget>[
                Panel(
                  section: Section.family,
                  title: names[fees.studentId] ?? '#${fees.studentId}',
                  children: <Widget>[
                    Fact(
                      label: s.grossCharges,
                      value: Fmt.money(fees.grossCharges, fees.currency, s.lang),
                      icon: Icons.request_quote_rounded,
                      numeric: true,
                    ),
                    Fact(
                      label: s.discounts,
                      value: Fmt.money(fees.discounts, fees.currency, s.lang),
                      icon: Icons.local_offer_rounded,
                      iconColor: AppColors.success,
                      numeric: true,
                    ),
                    const Divider(height: 18),
                    Fact(
                      label: s.balance,
                      value: Fmt.money(fees.position, fees.currency, s.lang),
                      icon: Icons.account_balance_wallet_rounded,
                      iconColor: fees.position > 0
                          ? AppColors.danger
                          : AppColors.success,
                      numeric: true,
                      emphasis: true,
                    ),
                  ],
                ),
                const SizedBox(height: 12),
              ],
            ],
          );
        },
      ),
    );
  }
}
