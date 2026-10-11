cask "git-chunk" do
  version "1.2.4"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "f0d2e2c68a6a7aaa24d58795cca1f9449fdcd63beeb7db5ab56591e4b6b2e04b", intel: "80d99453d98b235d4420700aef4f7eb6291a9f4e3d7570bd99ed25ec2b965b4c"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{version}/GitChunk-osx-#{arch}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{appdir}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
