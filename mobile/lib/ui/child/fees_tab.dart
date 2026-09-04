import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../l10n/strings.dart';
import '../../models/portal.dart';
import '../../state/auth_controller.dart';
import '../format.dart';
import '../widgets/async_view.dart';
import '../widgets/panels.dart';

/// The family's money for one student.
///
/// Gross, discounts and the position are all shown because BR-DIS-010 forbids
/// netting a discount away invisibly — a parent is entitled to see what was
/// charged and what was taken off it, not only the difference. None of the
/// three is computed here: `IFeeAdmin.ComputeStudentPositionAsync` is the
/// single central computation BR-FEE-008 requires, and it already ran.
class FeesTab extends StatelessWidget {
  const FeesTab({required this.studentId, super.key});

  final int studentId;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final AuthController auth = context.watch<AuthController>();

    return AsyncView<PortalFees>(
      load: () => auth.api.fees(studentId),
      builder: (BuildContext context, PortalFees fees) {
        return ListView(
          padding: const EdgeInsets.all(16),
          children: <Widget>[
            Panel(
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
                const Divider(height: 20),
                Fact(
                  label: s.balance,
                  value: Fmt.money(fees.position, fees.currency),
                  numeric: true,
                  emphasis: true,
                ),
              ],
            ),
            const SizedBox(height: 12),
            Panel(
              title: s.postedCharges,
              children: <Widget>[
                if (fees.charges.isEmpty)
                  Text(
                    s.noCharges,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                        ),
                  )
                else
                  for (final PortalChargeLine line in fees.charges)
                    Fact(
                      label: '${s.chargeNo} ${line.chargeNo}\n'
                          '${Fmt.date(line.postedAtUtc, s.isArabic ? 'ar' : 'en')}',
                      value: Fmt.money(line.grossAmount, fees.currency),
                      numeric: true,
                    ),
              ],
            ),
          ],
        );
      },
    );
  }
}
