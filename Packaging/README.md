# Binary distribution

Run the **Publish Git Chunk** GitHub Actions workflow from `main` with version `1.0.0`. It builds and tests Windows and both macOS architectures before publishing a release with the description in [RELEASE-NOTES.md](RELEASE-NOTES.md). Existing releases are not overwritten.

The workflow builds the Windows installer and portable ZIP, macOS Apple Silicon and Intel ZIPs, and SHA256SUMS. Scoop and Homebrew manifests are generated from the exact asset checksums and committed to `main`. Repository rules must allow that push; concurrent commits cause a safe rejection.

This release resets the previous trial version numbering to **v1.0.0**. It includes the current LFS conversion, update, exclusion-list, and legacy-data compatibility features. Users of old 1.1.x or 1.2.x trial builds must reinstall rather than use automatic update to move to 1.0.0. Back up or deactivate their old active update cache before applying the new build, because cached higher versions take precedence.

Users need Git, but no .NET SDK. The Windows installer adds a Start menu entry and can register `git chunk` on the user PATH without administrator permissions. Open a new terminal after installation. On macOS, select the ZIP matching the processor and move GitChunk.app to Applications, or use the project's Homebrew tap.

Apply Git Chunk once per repository and computer with `git chunk install`, or select the project in the app and click **Install Git Chunk**. Commit the initial configuration, then use Git as usual. Existing LFS conversion requires committed or stashed tracked changes; inspect the prepared changes before committing and pushing. Past LFS data and history stay in place.

Builds are not publisher-signed or Apple-notarized. OS trust prompts may appear. The project provides its own Scoop bucket and Homebrew tap; no public winget, Homebrew core, or npm registry entry is claimed.

For installation commands, see the [README](../README.md). Workflow results and release assets provide the verification record for each publication; do not treat an uncompleted workflow as a published release.
