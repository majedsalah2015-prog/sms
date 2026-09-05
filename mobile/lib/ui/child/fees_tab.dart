import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../l10n/strings.dart';
import '../../models/portal.dart';
import '../../state/auth_controller.dart';
import '../format.dart';
import '../theme.dart';
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
        final bool owing = fees.position > 0;
        return ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: <Widget>[
            Panel(
              children: <Widget>[
                BigStat(
                  section: Section.fees,
                  label: owing ? s.outstanding : s.balance,
                  value: owing
                      ? Fmt.money(fees.position, fees.currency, s.lang)
                      : s.settled,
                ),
                const Divider(height: 24),
                Fact(
                  label: s.grossCharges,
                  value: Fmt.money(fees.grossCharges, fees.currency, s.lang),
                  icon: Icons.request_quote_rounded,
                  iconColor: AppColors.muted,
                  numeric: true,
                ),
                Fact(
                  label: s.discounts,
                  value: Fmt.money(fees.discounts, fees.currency, s.lang),
                  icon: Icons.local_offer_rounded,
                  // Reported apart and never netted invisibly (BR-DIS-010).
                  iconColor: AppColors.success,
                  numeric: true,
                ),
              ],
            ),
            const SizedBox(height: 12),
            Panel(
              section: Section.fees,
              title: s.postedCharges,
              subtitle: fees.charges.isEmpty
                  ? null
                  : '${fees.charges.length} · ${fees.currency}',
              children: <Widget>[
                if (fees.charges.isEmpty)
                  Text(
                    s.noCharges,
                    style: Theme.of(context)
                        .textTheme
                        .bodyMedium
                        ?.copyWith(color: AppColors.muted),
                  )
                else
                  for (int i = 0; i < fees.charges.length; i++) ...<Widget>[
                    if (i > 0) const Divider(height: 18),
                    _ChargeRow(line: fees.charges[i], currency: fees.currency),
                  ],
              ],
            ),
          ],
        );
      },
    );
  }
}

class _ChargeRow extends StatelessWidget {
  const _ChargeRow({required this.line, required this.currency});

  final PortalChargeLine line;
  final String currency;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);

    return Row(
      children: <Widget>[
        Container(
          width: 34,
          height: 34,
          decoration: BoxDecoration(
            color: Section.fees.wash,
            borderRadius: BorderRadius.circular(10),
          ),
          child: Icon(Icons.receipt_rounded,
              size: 17, color: Section.fees.color),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Directionality(
                textDirection: TextDirection.ltr,
                child: Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: Text(
                    line.chargeNo,
                    style: theme.textTheme.bodyMedium
                        ?.copyWith(fontWeight: FontWeight.w500),
                  ),
                ),
              ),
              Text(
                Fmt.date(line.postedAtUtc, s.lang),
                style: theme.textTheme.bodySmall
                    ?.copyWith(color: AppColors.muted),
              ),
            ],
          ),
        ),
        Directionality(
          textDirection: TextDirection.ltr,
          child: Text(
            Fmt.money(line.grossAmount, currency, s.lang),
            style: theme.textTheme.bodyLarge
                ?.copyWith(fontWeight: FontWeight.w600),
          ),
        ),
      ],
    );
  }
}
