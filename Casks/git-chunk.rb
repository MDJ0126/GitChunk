cask "git-chunk" do
  version "1.2.2"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "d948907cccd69b41ce5b622aa40f2b36dd12e159be5a9e1d79769dab2e91ddc6", intel: "8043d93a74a5de8d7f392e7b032ed939bdfc65bf7ad82351980959a1868b8b57"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{version}/GitChunk-osx-#{arch}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{appdir}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
