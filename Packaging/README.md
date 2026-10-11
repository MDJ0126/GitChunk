# Binary distribution

Run the **Publish Git Chunk** GitHub Actions workflow from `main` with a version such as `1.0.0`. It builds and tests Windows and both Mac architectures before creating a GitHub Release. An existing version fails instead of overwriting its assets. It then commits generated `bucket` and `Casks` manifests to `main`. Repository rules must allow the workflow to push; concurrent commits cause a safe rejection instead of overwriting work.

Users need Git, but no .NET SDK. The Windows setup adds a Start menu entry and registers `git chunk` on the user PATH without administrator permissions. Open a new terminal after installation. Uninstall leaves repository configuration and data untouched; its PATH entry remains harmless and can be removed manually.

v1.0.0 is published. With the package manager already installed:

```powershell
# With Scoop already installed:
scoop bucket add gitchunk https://github.com/MDJ0126/GitChunk.git
scoop install gitchunk/git-chunk
```

```bash
# With Homebrew already installed:
brew tap MDJ0126/gitchunk https://github.com/MDJ0126/GitChunk
brew install --cask MDJ0126/gitchunk/git-chunk
```

Then run `git chunk install` in each project. The release assets and manifests are published. Windows installer installation, CLI execution and uninstall have been tested; both Mac architectures passed native build and data tests. Complete Scoop/Homebrew installation on a user's computer remains unverified.

Windows users can also download `GitChunk-Setup-win-x64.exe` from Releases and double-click it. Mac users can download their architecture's ZIP and move `GitChunk.app` to Applications. Builds are not publisher-signed or Apple-notarized. OS trust prompts may appear; package availability does not imply code signing.

No public winget, Homebrew core, or npm registry entry is claimed. These are the project's own package feeds.
