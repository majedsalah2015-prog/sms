import 'dart:io';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../l10n/strings.dart';
import '../state/auth_controller.dart';
import 'widgets/auth_scaffold.dart';

/// Sign-in.
///
/// Nothing is decided here. Lockout (BR-SEC-002), the second factor
/// (BR-SEC-003), the forced first change (BR-SEC-005) and every audit event
/// belong to `IAuthenticationService`; this screen posts a username and a
/// password and shows whatever comes back, already translated.
class LoginPage extends StatefulWidget {
  const LoginPage({super.key});

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final GlobalKey<FormState> _form = GlobalKey<FormState>();
  final TextEditingController _userName = TextEditingController();
  final TextEditingController _password = TextEditingController();
  final TextEditingController _baseUrl = TextEditingController();

  bool _busy = false;
  bool _obscure = true;
  bool _showAddress = false;
  Object? _error;

  @override
  void initState() {
    super.initState();
    _baseUrl.text = context.read<AuthController>().baseUrl;
  }

  @override
  void dispose() {
    _userName.dispose();
    _password.dispose();
    _baseUrl.dispose();
    super.dispose();
  }

  /// What the school's session list shows beside this login. A native client's
  /// `User-Agent` is a library's name and tells an administrator nothing, which
  /// is why the API prefers this field over it.
  String _deviceName() {
    final String platform = Platform.operatingSystem;
    return 'SMS Portal ($platform)';
  }

  Future<void> _submit() async {
    if (!(_form.currentState?.validate() ?? false)) return;
    final AuthController auth = context.read<AuthController>();

    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await auth.setBaseUrl(_baseUrl.text);
      await auth.signIn(
        userName: _userName.text.trim(),
        password: _password.text,
        deviceName: _deviceName(),
      );
      // On success this widget is replaced by the stage switch in `app.dart`;
      // there is nothing left here to set state on.
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
    final AuthController auth = context.watch<AuthController>();

    return AuthScaffold(
      title: s.signIn,
      subtitle: auth.endedByServer ? s.sessionEnded : null,
      error: _error,
      children: <Widget>[
        Form(
          key: _form,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              TextFormField(
                controller: _userName,
                autofillHints: const <String>[AutofillHints.username],
                textInputAction: TextInputAction.next,
                decoration: InputDecoration(
                  labelText: s.userName,
                  prefixIcon: const Icon(Icons.person_outline_rounded),
                ),
                validator: (String? v) =>
                    (v ?? '').trim().isEmpty ? s.userNameRequired : null,
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _password,
                obscureText: _obscure,
                autofillHints: const <String>[AutofillHints.password],
                textInputAction: TextInputAction.done,
                onFieldSubmitted: (_) => _busy ? null : _submit(),
                decoration: InputDecoration(
                  labelText: s.password,
                  prefixIcon: const Icon(Icons.lock_outline_rounded),
                  suffixIcon: IconButton(
                    icon: Icon(
                      _obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined,
                    ),
                    onPressed: () => setState(() => _obscure = !_obscure),
                  ),
                ),
                validator: (String? v) =>
                    (v ?? '').isEmpty ? s.passwordRequired : null,
              ),
              const SizedBox(height: 12),
              Align(
                alignment: AlignmentDirectional.centerStart,
                child: TextButton.icon(
                  onPressed: () => setState(() => _showAddress = !_showAddress),
                  icon: Icon(
                    _showAddress ? Icons.expand_less : Icons.expand_more,
                  ),
                  label: Text(s.serverAddress),
                ),
              ),
              if (_showAddress)
                TextFormField(
                  controller: _baseUrl,
                  keyboardType: TextInputType.url,
                  // A URL is not Arabic text, and letting it flip in an RTL
                  // layout makes "http://10.0.2.2:5099" unreadable.
                  textDirection: TextDirection.ltr,
                  decoration: InputDecoration(
                    labelText: s.serverAddress,
                    prefixIcon: const Icon(Icons.dns_outlined),
                    helperText: s.serverAddressHint,
                    helperMaxLines: 2,
                  ),
                  validator: (String? v) {
                    final Uri? uri = Uri.tryParse((v ?? '').trim());
                    final bool ok = uri != null &&
                        uri.hasScheme &&
                        (uri.isScheme('http') || uri.isScheme('https')) &&
                        uri.host.isNotEmpty;
                    return ok ? null : s.serverAddressInvalid;
                  },
                ),
              const SizedBox(height: 24),
              FilledButton(
                onPressed: _busy ? null : _submit,
                child: _busy
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: <Widget>[
                          Text(s.signIn),
                          const SizedBox(width: 8),
                          const Icon(Icons.login_rounded, size: 18),
                        ],
                      ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
