#!/bin/bash
set -euo pipefail
if [[ "$(uname -s)" != "Darwin" ]]; then echo 'This installer targets macOS. Use Install.ps1 on Windows.' >&2; exit 1; fi
command -v git >/dev/null || { echo 'Git is required.' >&2; exit 1; }
command -v dotnet >/dev/null || { echo '.NET 9 SDK is required for installation from source.' >&2; exit 1; }
repo="${1:-$PWD}"
repo="$(cd "$repo" && pwd -P)"
actual="$(git -C "$repo" rev-parse --show-toplevel)"
[[ "$repo" == "$actual" ]] || { echo 'Select the Git repository root.' >&2; exit 1; }
work="$(mktemp -d "${TMPDIR:-/tmp}/GitChunk-install.XXXXXX")"
if [[ -n "${GITCHUNK_SOURCE:-}" ]]; then
  source_root="$(cd "$GITCHUNK_SOURCE" && pwd -P)"
else
  source_root="$work/source"
  git clone --depth 1 https://github.com/MDJ0126/GitChunk.git "$source_root"
fi
case "$(uname -m)" in arm64) rid=osx-arm64 ;; x86_64) rid=osx-x64 ;; *) echo 'Unsupported CPU architecture.' >&2; exit 1 ;; esac
dotnet publish "$source_root/Tools/ResourceSplitter/ResourceSplitter.csproj" -c Release -r "$rid" -p:Headless=true --self-contained true -o "$work/publish"
install_dir="${GITCHUNK_INSTALL_DIR:-$HOME/.local/bin}"
mkdir -p "$install_dir"
cp "$work/publish/GitChunk" "$install_dir/git-chunk"
chmod +x "$install_dir/git-chunk"
"$install_dir/git-chunk" install "$repo"
echo "Installed command: $install_dir/git-chunk"
echo "Add this directory to PATH if needed: export PATH=\"$install_dir:\$PATH\""
echo 'Use: git chunk install / git chunk status / git chunk rules'
echo "Build files retained at: $work"
