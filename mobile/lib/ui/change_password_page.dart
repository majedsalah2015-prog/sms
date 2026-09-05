import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../l10n/strings.dart';
import '../state/auth_controller.dart';
import 'widgets/auth_scaffold.dart';

/// BR-SEC-005.
///
/// The password policy is the school's and lives on the server, which answers
/// `422 password_policy` with a sentence per broken rule, already in the
/// caller's language. This screen therefore validates only what it can know
/// without asking — that the two boxes match — and shows the server's reasons
/// for everything else. A client-side copy of the policy is a copy that goes
/// stale the day a school tightens it.
class ChangePasswordPage extends StatefulWidget {
  const ChangePasswordPage({this.forced = false, super.key});

  /// True when this is the forced first change and there is no way past it.
  final bool forced;

  @override
  State<ChangePasswordPage> createState() => _ChangePasswordPageState();
}

class _ChangePasswordPageState extends State<ChangePasswordPage> {
  final GlobalKey<FormState> _form = GlobalKey<FormState>();
  final TextEditingController _current = TextEditingController();
  final TextEditingController _next = TextEditingController();
  final TextEditingController _confirm = TextEditingController();

  bool _busy = false;
  Object? _error;

  @override
  void dispose() {
    _current.dispose();
    _next.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_form.currentState?.validate() ?? false)) return;
    final AuthController auth = context.read<AuthController>();
    final NavigatorState navigator = Navigator.of(context);
    final ScaffoldMessengerState messenger = ScaffoldMessenger.of(context);
    final String done = Strings.of(context).passwordChanged;

    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await auth.changePassword(
        currentPassword: _current.text,
        newPassword: _next.text,
      );
      if (!mounted) return;
      messenger.showSnackBar(SnackBar(content: Text(done)));
      // The forced case is replaced by the stage switch in `app.dart`; a
      // voluntary change was pushed onto a stack and pops back.
      if (!widget.forced && navigator.canPop()) navigator.pop();
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
      icon: Icons.lock_reset_rounded,
      title: s.changePasswordTitle,
      subtitle: widget.forced ? s.changePasswordPrompt : null,
      error: _error,
      children: <Widget>[
        Form(
          key: _form,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              TextFormField(
                controller: _current,
                obscureText: true,
                textInputAction: TextInputAction.next,
                decoration: InputDecoration(
                  labelText: s.currentPassword,
                  prefixIcon: const Icon(Icons.lock_outline_rounded),
                ),
                validator: (String? v) =>
                    (v ?? '').isEmpty ? s.passwordRequired : null,
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _next,
                obscureText: true,
                textInputAction: TextInputAction.next,
                decoration: InputDecoration(
                  labelText: s.newPassword,
                  prefixIcon: const Icon(Icons.lock_reset_rounded),
                ),
                validator: (String? v) =>
                    (v ?? '').isEmpty ? s.passwordRequired : null,
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _confirm,
                obscureText: true,
                textInputAction: TextInputAction.done,
                onFieldSubmitted: (_) => _busy ? null : _submit(),
                decoration: InputDecoration(
                  labelText: s.confirmPassword,
                  prefixIcon: const Icon(Icons.check_circle_outline_rounded),
                ),
                validator: (String? v) =>
                    v == _next.text ? null : s.passwordsDoNotMatch,
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
                    : Text(s.save),
              ),
              if (widget.forced) ...<Widget>[
                const SizedBox(height: 8),
                TextButton(
                  onPressed: _busy
                      ? null
                      : () => context.read<AuthController>().signOut(),
                  child: Text(s.signOut),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}
