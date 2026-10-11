# Git Chunk

**English** · [한국어](README.ko.md)

Store large files as chunks in ordinary Git. Install once per repository and computer, then use `git add`, commit, push and pull as usual. Local files remain full originals. No Git LFS server or separate storage account is required. Chunking does **not** reduce total data size.

## Windows app

For ready-to-install distribution (no SDK), download the [Windows installer or Mac app](https://github.com/MDJ0126/GitChunk/releases/latest), or use [Scoop and Homebrew](Packaging/README.md). Windows, Apple Silicon and Intel Mac builds are published starting with v1.0.0.

Run **GitChunk.exe**, select the repository root, edit your chunk rules or load a preset, and click **Install Git Chunk**. The UI defaults to English and shows installation status. An installed repository offers **Update / Reinstall**.

After first installation, or after changing the policy for existing tracked files:

```bash
git add -u
git add --renormalize .
git add .gitattributes .gitignore .gitchunk
git commit -m "Configure Git Chunk"
git push
```

On a new computer, clone the repository and install once. Installation restores unchanged pointer files from the clone. The app has no manual split/restore menus.

## Command-line installation

You can install without manually downloading an EXE or opening the GUI. The source installer fetches this repository, builds a self-contained CLI, installs it, and configures your selected repository. **Git and a .NET SDK capable of building .NET 9 are required.** A binary still runs internally; this is not a built-in Git command or a package-manager release.

### Windows / PowerShell

Download the script and run it against your project:

```powershell
Invoke-WebRequest https://raw.githubusercontent.com/MDJ0126/GitChunk/main/Install.ps1 -OutFile Install.ps1
powershell -ExecutionPolicy Bypass -File ./Install.ps1 -Repository C:/path/to/project
```

It installs `git-chunk.exe` under `%LOCALAPPDATA%/GitChunk/bin` and adds that directory to your user PATH. Open a new terminal:

```bash
git chunk install
git chunk status
git chunk rules
```

Each command also accepts a repository-root argument. `install` configures the current repository; `status` reports `installed`, `legacy`, `incomplete` or a validation state; `rules` prints the active policy.

For a local checkout, without downloading source again:

```powershell
./Install.ps1 -Repository C:/path/to/project -SourcePath C:/path/to/GitChunk
```

`-InstallDirectory` overrides the CLI directory. `-NoPathChanges` skips PATH changes, useful for isolated testing. The installer keeps its temporary source/build directory for diagnostics.

### macOS / Terminal

```bash
curl -fL https://raw.githubusercontent.com/MDJ0126/GitChunk/main/install.sh -o install-gitchunk.sh
bash install-gitchunk.sh /path/to/project
export PATH="$HOME/.local/bin:$PATH"
git chunk status
```

Add the PATH line to your shell profile if needed. The installer builds the native Apple Silicon or Intel CLI; no SwiftUI app build is required. `GITCHUNK_SOURCE` can point to a local checkout, and `GITCHUNK_INSTALL_DIR` overrides the command directory. The Mac installer and GUI still require execution verification on an actual Mac.

These source-build scripts remain available for development. For ordinary installation, use the published release or the project's Scoop/Homebrew feed. There is no public winget or Homebrew core registry entry.

## Rules and presets

The active policy is `.gitchunk/rules.txt`. Presets live in `.gitchunk/presets/*.txt`. Edit them directly, or use **Save rules**, **Load**, and **Save preset** in the app. Loading a preset fills the editor; click Save rules to apply it. Installation also saves the current editor contents.

```gitignore
# Include assets and selected file types
Assets/**
*.psd
*.wav
# Exclude a subtree; the last matching rule wins
!Assets/Small/**
```

- Only matching files **larger than 50 MiB** become chunks of at most 49 MiB.
- `*` matches within a path segment, `**` spans directories, and `?` matches one character.
- A pattern without `/` matches names at any depth. `/Assets/**` anchors to the root. A trailing `/` matches a directory subtree.
- `#` begins a comment, `!` excludes a match, and the last matching rule wins. Unmatched files are excluded.
- `default` includes all large files; `unity` targets Assets; `unreal` targets Content.
- This is a limited .gitignore-like syntax. Escapes and character classes are not supported. Use `/` for paths.
- `.meta`, Git settings, the tool, and both storage directories are always excluded.
- Excluded files remain ordinary Git files. Commit/push guards reject ordinary blobs of 100 MiB or more.

Changing a policy never prevents restoration of an existing pointer. To change how already tracked files are stored, renormalize and commit. Commit rules and presets to share them with teammates. Existing custom rules and presets are not replaced just to change UI language.

```text
.gitchunk/
  rules.txt
  presets/default.txt
  presets/unity.txt
  presets/unreal.txt
  presets/my-project.txt
  GitChunk.exe                   # Windows installation tool
  objects/<blob-id>              # Actual file data
```

## Storage and legacy migration

The clean filter stores a small pointer at the original Git path. The pre-commit hook adds only chunks referenced by staged pointers. The smudge filter verifies chunk and full-file SHA-256 hashes and restores the original. Do not delete chunk data.

ResourceSplitter pointer-v1 and historical `.resourcesplitter` commits remain readable. Installing Git Chunk verifies and copies local legacy chunks into `.gitchunk/objects`, then archives the old directory under Git's local metadata. Unknown legacy files are preserved in that backup. If a legacy application keeps the directory locked, close it and reinstall to finish archiving.

The active executable also lives in Git metadata, so checking out a commit from before installation cannot remove the active filter. Legacy filter names and the pointer protocol remain supported for history compatibility. Recovery-only legacy split/join CLI commands remain available; the app UI is installation-only. Existing raw/LFS history is not automatically rewritten.

## Build and verification

Windows GUI:

```powershell
dotnet publish Tools/ResourceSplitter/ResourceSplitter.csproj -c Release -r win-x64 -o publish/windows
```

Windows CLI only:

```powershell
dotnet publish Tools/ResourceSplitter/ResourceSplitter.csproj -c Release -r win-x64 -p:Headless=true -o publish/cli
```

Mac GUI (macOS 13+, Xcode Command Line Tools, .NET SDK):

```bash
bash Tools/ResourceSplitter/Mac/Build.sh osx-arm64
bash Tools/ResourceSplitter/Mac/Build.sh osx-x64
```

Output: `Tools/ResourceSplitter/bin/mac/<RID>/GitChunk.app` and ZIP. Mac packages use local ad-hoc signing. GUI compilation/execution and the source installer on macOS are not yet verified on a Mac. Shared Apple Silicon and Intel backends are cross-buildable on Windows.

GitHub Actions **Build Git Chunk** is manual-only. Integration checks cover rules, presets, normal blobs, legacy migration, clone/pull/checkout, stash, GC and corrupted-data rejection. Source-directory and project-file names remain ResourceSplitter for path compatibility; product and storage names are Git Chunk and `.gitchunk`.

PowerShell GUI-executable commands need `| Out-Host` when output and completion must be awaited. The headless CLI installed by Install.ps1 runs as a console executable.

## 한국어 요약

앱 기본 언어는 영어입니다. 저장소 최상위 폴더를 선택하고 **Install Git Chunk**를 누르면 설치와 최초 원본 복원을 처리합니다. 규칙은 `.gitchunk/rules.txt`, 프리셋은 `.gitchunk/presets`에 보관합니다.

명령어 설치 스크립트는 EXE를 직접 내려받을 필요 없이 소스를 받아 CLI를 빌드·설치합니다. 현재 Git과 .NET SDK가 필요하며, 내부 실행 파일은 자동 설치됩니다. 스크립트를 커밋·푸시한 뒤 위 다운로드 명령을 사용할 수 있습니다.

기존 `.resourcesplitter` 조각은 검증 후 `.gitchunk`로 통합하고, 이전 폴더 전체는 Git 내부에 백업합니다. 과거 커밋 복원은 유지합니다. 자세한 한국어 설명은 [README.ko.md](README.ko.md)를 참고하세요.
