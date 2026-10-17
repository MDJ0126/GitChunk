cask "git-chunk" do
  version "1.0.0"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "1759799c116ae608ba4fe6495b8dedfa8e38bde51a453bf85fa070b5308843f0", intel: "36e431f9aba9a43affe6dd1efdf439bc099210f6b24912de8f695444a6559d72"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{version}/GitChunk-osx-#{arch}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{appdir}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
