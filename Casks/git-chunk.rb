cask "git-chunk" do
  version "1.1.0"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "d0ad429d544c13c0420c37ffbe4378bee0f5862499a2f6847867247f2706d06a", intel: "fd4c854df8551ef82ee64c478f6d9eb6828b91d9c621b057025586195c754ef5"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{version}/GitChunk-osx-#{arch}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{appdir}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
