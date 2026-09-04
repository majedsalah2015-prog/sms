import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../l10n/strings.dart';
import '../state/auth_controller.dart';
import 'widgets/auth_scaffold.dart';

/// BR-SEC-003, second step.
///
/// The five-minute ticket held by [AuthController] is proof the password was
/// accepted and grants nothing on its own — without it a caller who guessed an
/// account id could attack the second factor alone.
class TwoFactorPage extends StatefulWidget {
  const TwoFactorPage({super.key});

  @override
  State<TwoFactorPage> createState() => _TwoFactorPageState();
}

class _TwoFactorPageState extends State<TwoFactorPage> {
  final GlobalKey<FormState> _form = GlobalKey<FormState>();
  final TextEditingController _code = TextEditingController();

  bool _busy = false;
  Object? _error;

  @override
  void dispose() {
    _code.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_form.currentState?.validate() ?? false)) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await context.read<AuthController>().submitTwoFactor(_code.text.trim());
    } on Object catch (e) {
      if (!mounted) return;
      setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);

    return AuthScaffold(
      title: s.twoFactorTitle,
      subtitle: s.twoFactorPrompt,
      error: _error,
      children: <Widget>[
        Form(
          key: _form,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              TextFormField(
                controller: _code,
                autofocus: true,
                keyboardType: TextInputType.number,
                inputFormatters: <TextInputFormatter>[
                  FilteringTextInputFormatter.digitsOnly,
                  LengthLimitingTextInputFormatter(8),
                ],
                // A TOTP code is digits, and Arabic-Indic numerals would not be
                // what the authenticator app is showing.
                textDirection: TextDirection.ltr,
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 24, letterSpacing: 6),
                onFieldSubmitted: (_) => _busy ? null : _submit(),
                decoration: InputDecoration(labelText: s.twoFactorCode),
                validator: (String? v) =>
                    (v ?? '').trim().isEmpty ? s.twoFactorRequired : null,
              ),
              const SizedBox(height: 24),
              FilledButton(
                onPressed: _busy ? null : _submit,
                child: _busy
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Text(s.verify),
              ),
              const SizedBox(height: 8),
              TextButton(
                onPressed: _busy
                    ? null
                    : () => context.read<AuthController>().signOut(),
                child: Text(s.cancel),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
