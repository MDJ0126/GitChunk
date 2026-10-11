cask "git-chunk" do
  version "1.1.1"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "647208deb8d238cecf42b5dffd4dcbbc57bb428942bc9e8f321ea646ec650027", intel: "f3edc5ca458666b6334b902eb5ee472020047d57356921543c46eb3cb7608874"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{version}/GitChunk-osx-#{arch}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{appdir}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
