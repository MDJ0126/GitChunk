cask "git-chunk" do
  version "1.0.0"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "0cd201030dbae59789cee9cf294526abf86e742fe3f78ae876682d0a8c38543a", intel: "bf114acf871d10f935fb022f66b2a1610d2acfa8c8193e636b5a2db53b6702ea"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{version}/GitChunk-osx-#{arch}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{appdir}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
