cask "git-chunk" do
  version "1.0.0"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "3bcc218467253713310b248c9843e1e9a5ed0497d8985fab47586d89b55911a7", intel: "b00baecc31c6c97a4f612b11a0ad9310a15e2e572c2853fd78e0ad872e2925d2"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{version}/GitChunk-osx-#{arch}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{appdir}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
