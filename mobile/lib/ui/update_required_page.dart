import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../l10n/strings.dart';
import '../state/auth_controller.dart';
import '../state/update_controller.dart';
import 'widgets/auth_scaffold.dart';
import 'widgets/update_banner.dart';

/// The school no longer accepts this build.
///
/// It stands in front of everything, sign-in included, because that is what
/// distinguishes it from the banner: an app that has stopped being supported
/// cannot be trusted to show a family their child's marks correctly, and a
/// demand that can be scrolled past is not a demand. The server only ever asks
/// for this when a build new enough to satisfy it is genuinely published, so the
/// family is never shut out of something they cannot fix.
///
/// It keeps the sign-in frame — the school's mark, the language switch, and the
/// running version printed at the bottom — because the last of those is the
/// first thing a family will be asked for when they call the school to say the
/// update did not work.
class UpdateRequiredPage extends StatelessWidget {
  const UpdateRequiredPage({super.key});

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final AuthController auth = context.watch<AuthController>();
    final UpdateController update = context.read<UpdateController>();

    return AuthScaffold(
      icon: Icons.system_update_rounded,
      title: s.updateRequiredTitle,
      subtitle: s.updateRequiredBody,
      children: <Widget>[
        const Center(child: VersionStep()),
        const SizedBox(height: 18),
        FilledButton.icon(
          onPressed: () => openInstallPage(context),
          icon: const Icon(Icons.download_rounded, size: 20),
          label: Text(s.updateNow),
        ),
        const SizedBox(height: 4),
        // A way out that does not need an install: a school that raised the floor
        // by mistake lowers it again on the server, and without this the family
        // would have to clear the app's data to find out.
        TextButton(
          onPressed: () => update.check(
            baseUrl: auth.baseUrl,
            languageCode: auth.languageCode,
          ),
          child: Text(s.retry),
        ),
      ],
    );
  }
}
