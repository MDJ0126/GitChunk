# Git Chunk

[한국어](README.ko.md)

Automatically store large files as chunks in Git while keeping full originals locally. Requires Git; no .NET SDK needed.

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

[Distribution details](Packaging/README.md) · [Technical notes](INSTRUCTIONS.md)
