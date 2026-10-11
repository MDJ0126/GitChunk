#define AppVersion GetEnv("GITCHUNK_VERSION")
[Setup]
AppId=GitChunk
AppName=Git Chunk
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\GitChunk
DefaultGroupName=Git Chunk
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=GitChunk-Setup-win-x64
SetupIconFile=..\Tools\GitChunk\Assets\AppIcon.ico
UninstallDisplayIcon={app}\GitChunk.exe
Compression=lzma2
SolidCompression=yes
ChangesEnvironment=yes
[Files]
Source: "..\publish\windows\GitChunk.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\cli\GitChunk.exe"; DestDir: "{app}\bin"; DestName: "git-chunk.exe"; Flags: ignoreversion
[Icons]
Name: "{group}\Git Chunk"; Filename: "{app}\GitChunk.exe"
[Tasks]
Name: "addpath"; Description: "Add git chunk to the command line (recommended)"
[Registry]
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}\bin"; Check: NeedsPath; Tasks: addpath
[Run]
Filename: "{app}\GitChunk.exe"; Description: "Open Git Chunk"; Flags: nowait postinstall skipifsilent
[Code]
function NeedsPath: Boolean;
var CurrentPath: String;
begin
  RegQueryStringValue(HKCU, 'Environment', 'Path', CurrentPath);
  Result := Pos(';' + Lowercase(ExpandConstant('{app}\bin')) + ';', ';' + Lowercase(CurrentPath) + ';') = 0;
end;
