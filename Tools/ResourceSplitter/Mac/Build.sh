#!/bin/bash
set -euo pipefail
script_dir="$(cd "$(dirname "$0")" && pwd)"
rid="${1:-osx-arm64}"
case "$rid" in
  osx-arm64) arch="arm64" ;;
  osx-x64) arch="x86_64" ;;
  *) echo 'Use osx-arm64 or osx-x64' >&2; exit 1 ;;
esac
out="$script_dir/../bin/mac/$rid"
app="$out/ResourceSplitter.app"
mkdir -p "$app/Contents/MacOS"
mkdir -p "$app/Contents/Resources"
iconutil -c icns "$script_dir/../Assets/AppIcon.iconset" -o "$app/Contents/Resources/AppIcon.icns"
cp "$script_dir/../Assets/AppIcon.png" "$app/Contents/Resources/AppIcon.png"
dotnet publish "$script_dir/../ResourceSplitter.csproj" -c Release -r "$rid" --self-contained true -o "$out/cli"
cp "$out/cli/ResourceSplitter" "$app/Contents/MacOS/ResourceSplitter-cli"
xcrun swiftc -swift-version 5 -parse-as-library -target "$arch-apple-macos13.0" -sdk "$(xcrun --show-sdk-path)" \
  -framework SwiftUI -framework AppKit "$script_dir/MacApp.swift" -o "$app/Contents/MacOS/ResourceSplitter"
cat > "$app/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>ResourceSplitter</string>
<key>CFBundleIdentifier</key><string>local.resourcesplitter.app</string>
<key>CFBundleName</key><string>Resource Splitter</string>
<key>CFBundleIconFile</key><string>AppIcon</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleVersion</key><string>1</string>
<key>CFBundleShortVersionString</key><string>1.0</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
chmod +x "$app/Contents/MacOS/ResourceSplitter" "$app/Contents/MacOS/ResourceSplitter-cli"
codesign --force --sign - "$app/Contents/MacOS/ResourceSplitter-cli"
codesign --force --deep --sign - "$app"
ditto -c -k --sequesterRsrc --keepParent "$app" "$out/ResourceSplitter-$rid.zip"
echo "Package: $out/ResourceSplitter-$rid.zip"
