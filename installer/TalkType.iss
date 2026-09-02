#define MyAppName "TalkType"
#define MyAppVersion "0.1.2-alpha"
#define MyAppPublisher "TalkType contributors"
#define MyAppExeName "TalkType.exe"
#ifndef MySourceDir
  #define MySourceDir "..\artifacts\TalkType-Windows-x64"
#endif

[Setup]
AppId={{D9D909C8-6EFF-4C02-A23B-C31A77751F5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=TalkType-Setup-{#MyAppVersion}-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern dynamic
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion=0.1.2.0
VersionInfoProductName={#MyAppName}
VersionInfoDescription=Private, local-first voice typing

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TalkType"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\TalkType"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch TalkType"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
