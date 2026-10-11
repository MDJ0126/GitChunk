cask "git-chunk" do
  version "1.2.0"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "7e755b9d23d097bd3fb9722921a0fae88d479fde62b8942ddb595f0e99417635", intel: "9eca0d8f7b2c1c750ac4396ab5a1f8de838dabe4a205787d15c93e87bf952a31"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{version}/GitChunk-osx-#{arch}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{appdir}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
