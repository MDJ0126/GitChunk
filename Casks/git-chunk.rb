cask "git-chunk" do
  version "1.2.1"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "ddc9797565b07f6a4166b43d32e52337b6dae281291030a51a5b9d2dc6e999da", intel: "0675c91f163a645e29f9dca0b4e83cb9158af9b592e2819cbaf0563d3e65d78e"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{version}/GitChunk-osx-#{arch}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{appdir}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
