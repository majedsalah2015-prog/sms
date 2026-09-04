import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../l10n/strings.dart';
import '../models/me.dart';
import '../models/portal.dart';
import '../state/auth_controller.dart';
import 'format.dart';
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
      appBar: AppBar(title: Text(s.statement)),
      body: AsyncView<PortalStatement>(
        load: () => auth.api.statement(),
        builder: (BuildContext context, PortalStatement statement) {
          return ListView(
            padding: const EdgeInsets.all(16),
            children: <Widget>[
              Panel(
                children: <Widget>[
                  Fact(
                    label: s.familyTotal,
                    value: statement.total == 0
                        ? s.settled
                        : Fmt.money(statement.total, statement.currency),
                    numeric: statement.total != 0,
                    emphasis: true,
                  ),
                ],
              ),
              const SizedBox(height: 12),
              for (final PortalFees fees in statement.students) ...<Widget>[
                Panel(
                  title: names[fees.studentId] ?? '#${fees.studentId}',
                  children: <Widget>[
                    Fact(
                      label: s.grossCharges,
                      value: Fmt.money(fees.grossCharges, fees.currency),
                      numeric: true,
                    ),
                    Fact(
                      label: s.discounts,
                      value: Fmt.money(fees.discounts, fees.currency),
                      numeric: true,
                    ),
                    Fact(
                      label: s.balance,
                      value: Fmt.money(fees.position, fees.currency),
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
