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
app="$out/GitChunk.app"
mkdir -p "$app/Contents/MacOS"
mkdir -p "$app/Contents/Resources"
iconutil -c icns "$script_dir/../Assets/AppIcon.iconset" -o "$app/Contents/Resources/AppIcon.icns"
cp "$script_dir/../Assets/AppIcon.png" "$app/Contents/Resources/AppIcon.png"
version="${GITCHUNK_VERSION:-1.0.0}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo 'Invalid version' >&2; exit 1; }
dotnet publish "$script_dir/../GitChunk.csproj" -c Release -r "$rid" -p:Version="$version" --self-contained true -o "$out/cli"
cp "$out/cli/GitChunk" "$app/Contents/MacOS/GitChunk-cli"
xcrun swiftc -swift-version 5 -parse-as-library -target "$arch-apple-macos13.0" -sdk "$(xcrun --show-sdk-path)" \
  -framework SwiftUI -framework AppKit "$script_dir/MacApp.swift" -o "$app/Contents/MacOS/GitChunk"
cat > "$app/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>GitChunk</string>
<key>CFBundleIdentifier</key><string>local.gitchunk.app</string>
<key>CFBundleName</key><string>Git Chunk</string>
<key>CFBundleIconFile</key><string>AppIcon</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleVersion</key><string>1</string>
<key>CFBundleShortVersionString</key><string>1.0</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
 /usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $version" "$app/Contents/Info.plist"
 /usr/libexec/PlistBuddy -c "Set :CFBundleVersion $version" "$app/Contents/Info.plist"
chmod +x "$app/Contents/MacOS/GitChunk" "$app/Contents/MacOS/GitChunk-cli"
codesign --force --sign - "$app/Contents/MacOS/GitChunk-cli"
codesign --force --deep --sign - "$app"
ditto -c -k --sequesterRsrc --keepParent "$app" "$out/GitChunk-$rid.zip"
echo "Package: $out/GitChunk-$rid.zip"
