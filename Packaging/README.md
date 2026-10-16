# Binary distribution

Run the **Publish Git Chunk** GitHub Actions workflow from `main` with version `1.0.0`. It builds and tests Windows and both macOS architectures before publishing a release with the description in [RELEASE-NOTES.md](RELEASE-NOTES.md). Existing releases are not overwritten.

The workflow builds the Windows installer and portable ZIP, macOS Apple Silicon and Intel ZIPs, and SHA256SUMS. Scoop and Homebrew manifests are generated from the exact asset checksums and committed to `main`. Repository rules must allow that push; concurrent commits cause a safe rejection.

This release resets the previous trial version numbering to **v1.0.0**. It includes the current LFS conversion, update, exclusion-list, and legacy-data compatibility features. Users of old 1.1.x or 1.2.x trial builds must reinstall rather than use automatic update to move to 1.0.0. Back up or deactivate their old active update cache before applying the new build, because cached higher versions take precedence.

Users need Git, but no .NET SDK. The Windows installer adds a Start menu entry and can register `git chunk` on the user PATH without administrator permissions. Open a new terminal after installation. On macOS, select the ZIP matching the processor and move GitChunk.app to Applications, or use the project's Homebrew tap.

Apply Git Chunk once per repository and computer with `git chunk install`, or select the project in the app and click **Install Git Chunk**. Commit the initial configuration, then use Git as usual. Existing LFS conversion requires committed or stashed tracked changes; inspect the prepared changes before committing and pushing. Past LFS data and history stay in place.

Builds are not publisher-signed or Apple-notarized. OS trust prompts may appear. The project provides its own Scoop bucket and Homebrew tap; no public winget, Homebrew core, or npm registry entry is claimed.

For installation commands, see the [English README](../README.en.md). Workflow results and release assets provide the verification record for each publication; do not treat an uncompleted workflow as a published release.

## Linux v1.0.0 platform addition

Build locally with `python3 Tools/GitChunk/Linux/Build.py linux-x64`. This creates `dist/GitChunk-linux-x64.zip` with a self-contained CLI, Python/Tk desktop, and per-user installer. No GitHub Actions run is needed. The desktop requires Python 3/Tk; the CLI only needs Git and the distribution's .NET native dependencies. The package targets glibc distributions, not Alpine/musl. It has been verified on Ubuntu 24.04 x64.

For the existing v1.0.0 release, upload the Linux ZIP, extend SHA256SUMS while preserving every existing entry, and append Linux installation/validation information to the release body. Preserve the existing tag and Windows/macOS asset IDs and hashes. Linux source is linked to its main-branch commit because the original v1.0.0 tag is unchanged.

Linux verification: run `Mac/Test.py`, `Test-Rules.py`, `Test-Uninstall.py`, `Test-Identity.py`, `Linux/Test-Lfs.py`, and `Linux/Test-Gui.py` against the extracted CLI/package. Run the cross-platform `Tests/Updater` fixture with a Linux 1.3.0 fixture executable; these test versions are never published. Use Xvfb for the GUI test on a headless Linux machine.
