cask "git-chunk" do
  version "1.2.3"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "8ed2e2386ede4088eee22819ff7d7156d891acbe261a2a98a5415d0bb287c2ba", intel: "0886af9ae0fa8b70d52589ba65a40e224c0ab3bdb7bf22e40def5d6d27d9b5d9"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{version}/GitChunk-osx-#{arch}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{appdir}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
