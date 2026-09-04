#!/usr/bin/env bash
#
# Fills in the platform folders `flutter create` owns, without touching a line
# of the app.
#
# `lib/`, `test/`, `pubspec.yaml`, `README.md` and `.gitignore` are written by
# hand and are checked in. `android/` and `ios/` are not: they are per-machine
# glue carrying a binary Gradle wrapper, and running `flutter create .` in place
# would rewrite the hand-written files alongside them. So this generates into a
# scratch directory and copies only what is missing.
#
# Safe to re-run: an existing android/ or ios/ is left alone unless --force.

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
flutter_bin="${FLUTTER_BIN:-/c/src/flutter/bin/flutter}"
org="com.school2028"
name="sms_portal"
force=0

for arg in "$@"; do
  case "$arg" in
    --force) force=1 ;;
    *) echo "unknown option: $arg" >&2; exit 2 ;;
  esac
done

if [ ! -x "$flutter_bin" ] && [ ! -f "$flutter_bin.bat" ]; then
  echo "flutter not found at $flutter_bin — set FLUTTER_BIN" >&2
  exit 1
fi

# Windows Defender flags dart.exe as a generic threat; until C:\src\flutter is
# excluded every command below dies with "cannot execute the specified program",
# which reads like a broken install rather than an antivirus decision.
if ! "$flutter_bin" --version >/dev/null 2>&1; then
  echo "flutter will not run. If this is Windows, check that Defender has not" >&2
  echo "quarantined dart.exe:  Add-MpPreference -ExclusionPath 'C:\\src\\flutter'" >&2
  exit 1
fi

scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT

echo "generating platform folders in $scratch ..."
"$flutter_bin" create \
  --project-name "$name" \
  --org "$org" \
  --platforms=android,ios \
  --no-pub \
  "$scratch/gen" >/dev/null

for platform in android ios; do
  if [ -d "$here/$platform" ] && [ "$force" -eq 0 ]; then
    echo "$platform/ already exists — left alone (pass --force to replace)"
    continue
  fi
  rm -rf "${here:?}/$platform"
  cp -r "$scratch/gen/$platform" "$here/$platform"
  echo "$platform/ written"
done

manifest="$here/android/app/src/main/AndroidManifest.xml"
if [ -f "$manifest" ]; then
  # The app is nothing but network calls; without this it fails on first tap
  # with a socket error that looks like the school being down.
  if ! grep -q 'android.permission.INTERNET' "$manifest"; then
    perl -0pi -e 's#(<manifest[^>]*>)#$1\n    <uses-permission android:name="android.permission.INTERNET"/>#' "$manifest"
    echo "manifest: INTERNET permission added"
  fi

  # Cleartext for the development hosts ONLY. Android has blocked plain HTTP by
  # default since API 28, and a school's deployment is HTTPS — a blanket
  # usesCleartextTraffic="true" would quietly permit plaintext there too, which
  # is a security decision made by accident.
  security_dir="$here/android/app/src/main/res/xml"
  mkdir -p "$security_dir"
  cat > "$security_dir/network_security_config.xml" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<!--
  Development only. 10.0.2.2 is the Android emulator's alias for the host
  machine, and localhost is a device running the server itself; everything else
  keeps Android's default, which is HTTPS or nothing. A school deployment is
  reached over TLS and needs no entry here.
-->
<network-security-config>
    <domain-config cleartextTrafficPermitted="true">
        <domain includeSubdomains="false">10.0.2.2</domain>
        <domain includeSubdomains="false">localhost</domain>
        <domain includeSubdomains="false">127.0.0.1</domain>
    </domain-config>
</network-security-config>
XML

  if ! grep -q 'networkSecurityConfig' "$manifest"; then
    perl -0pi -e 's#(<application\b)#$1 android:networkSecurityConfig="\@xml/network_security_config"#' "$manifest"
    echo "manifest: network security config attached"
  fi
fi

echo
echo "done. next:"
echo "  flutter pub get"
echo "  flutter test"
echo "  flutter run"
