Git Chunk 1.0.0 - Linux

Requires Git and a glibc-based distribution. Verified on Ubuntu 24.04 x64.
The .NET runtime is included. Alpine/musl is not supported by this package.
Desktop UI additionally requires Python 3 with Tk and a graphical desktop:
  Ubuntu/Debian: sudo apt install git python3-tk
  Fedora: sudo dnf install git python3-tkinter

Extract the ZIP, open a terminal in the extracted folder, then run:
  chmod +x GitChunk git-chunk install.sh
  ./install.sh

Launch Git Chunk from the application menu or run ~/.local/bin/gitchunk.
Add ~/.local/bin to PATH to use `git chunk` from any terminal.
For portable use, run ./GitChunk (desktop) or ./git-chunk (CLI).

In your repository root:
  git chunk install
  git chunk status
  git chunk update
  git chunk uninstall

Apply the initial configuration and commit it following the command output.
Install Git Chunk on each computer that uses the repository.
For existing LFS projects, commit or stash tracked changes before installation.
Past LFS history and server data are preserved.

To remove the desktop application, remove ~/.local/share/gitchunk,
~/.local/share/applications/gitchunk.desktop,
~/.local/share/icons/hicolor/256x256/apps/gitchunk.png, and the
~/.local/bin/git-chunk and ~/.local/bin/gitchunk symbolic links.
Run git chunk uninstall in repositories first if you are discontinuing use.
