cask "git-chunk" do
  version "1.0.0"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "d4a5a2e670d060cfaf538d38db5e62289fad9f5fc76d2441204c603da3cc797d", intel: "61a5d99189912492747d9e5383db29feaa4807d7f6a2646dc1fc663b7bcae829"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{version}/GitChunk-osx-#{arch}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{appdir}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
