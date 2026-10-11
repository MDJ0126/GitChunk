# Git Chunk

[한국어](README.ko.md)

**Manage large assets without Git LFS.** Git stores chunks automatically while you work with full originals locally. No .NET SDK needed.

## Install

**Windows** — [Download the installer](https://github.com/MDJ0126/GitChunk/releases/latest), or with Scoop:

```powershell
scoop bucket add gitchunk https://github.com/MDJ0126/GitChunk.git
scoop install gitchunk/git-chunk
```

**Mac** — with Homebrew:

```bash
brew tap MDJ0126/gitchunk https://github.com/MDJ0126/GitChunk
brew install --cask MDJ0126/gitchunk/git-chunk
```

## Use

Run once in your project's Git repository root, including after cloning on a new PC:

```bash
git chunk install
```

For existing tracked files, apply and commit the configuration:

```bash
git add -u
git add --renormalize .
git add .gitattributes .gitignore .gitchunk
git commit -m "Configure Git Chunk"
```

Then use Git as usual. The app also supports setup: select your project and click **Install Git Chunk**.

Edit `.gitchunk/rules.txt` or use the app to choose files. Matching files over **50 MiB** become chunks of up to **49 MiB**. Do not delete `.gitchunk/objects`.

## Convert an existing Git LFS project

**Automatic LFS conversion requires v1.1.0 or later.** v1.0.0 does not include this feature. To run `GitChunk.exe` directly, stay in the target repository root and replace the install command below with the actual executable path followed by `install`.

1. In the target repository root, commit or stash tracked changes first. Downloading missing originals requires Git LFS and remote access.
2. Run the commands below. Installation obtains missing originals, verifies size and SHA-256, converts LFS attributes and standard hooks, verifies conversion/restoration, and stages the result.

```bash
git chunk install
git diff --cached
git commit -m "Convert current LFS files to Git Chunk"
git push
```

Current LFS files **over 50 MiB** become chunks; files at or below that size become ordinary Git files. Large LFS files excluded by chunk rules or custom commit/push hooks stop conversion with instructions. Installation never commits or pushes automatically. Failed conversion restores the original index, settings and hooks.

**After conversion, edit originals and use add/commit/push as usual.** Adding files chunks them automatically; the commit hook includes required chunks. Pull/checkout restores originals automatically. On a new computer, clone and run `git chunk install` once.

**Past commits and existing server LFS objects/storage usage are preserved.** Keep Git LFS and the corresponding originals to use historical LFS versions.

[Distribution details](Packaging/README.md) · [Technical notes](INSTRUCTIONS.md)
