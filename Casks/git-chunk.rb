cask "git-chunk" do
  version "1.0.0"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "0dc597a0afefd6df74e844b3e6de7d74f8527b39a10cfbd862643d8d20ec003e", intel: "d65c35a0e7ead52010196b21dd6fcda1a7d7cad556304423c23a1b1d58fe760d"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{version}/GitChunk-osx-#{arch}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{appdir}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
