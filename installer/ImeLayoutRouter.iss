#ifndef Edition
  #define Edition "Full"
#endif
#if Edition == "Simple"
  #define EditionName "V1"
  #define AppVersion "1.1.0"
  #define EditionId "{{CB27A979-27AB-40E2-A613-52CC5EFCB128}"
#else
  #define EditionName "V2"
  #define AppVersion "2.0.0"
  #define EditionId "{{62EBC270-35B4-4E79-A170-426D38FEAA8C}"
#endif

[Setup]
AppId={#EditionId}
AppName=IME Layout Router {#EditionName}
AppVersion={#AppVersion}
AppPublisher=31916
DefaultDirName={localappdata}\Programs\IME Layout Router {#EditionName}
DefaultGroupName=IME Layout Router {#EditionName}
OutputDir=output
OutputBaseFilename=ImeLayoutRouter-{#EditionName}-{#AppVersion}-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\ImeLayoutRouter.exe

[Languages]
#if Edition == "Simple"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
#else
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
#endif

[CustomMessages]
#if Edition != "Simple"
english.DesktopShortcut=Create a desktop shortcut
english.Shortcuts=Additional shortcuts:
english.Launch=Launch IME Layout Router
#endif
japanese.DesktopShortcut=デスクトップにショートカットを作成する
japanese.Shortcuts=追加のショートカット:
japanese.Launch=IME Layout Router を起動する

[Files]
Source: "..\bin\{#Edition}\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\IME Layout Router {#EditionName}"; Filename: "{app}\ImeLayoutRouter.exe"
Name: "{userdesktop}\IME Layout Router {#EditionName}"; Filename: "{app}\ImeLayoutRouter.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; GroupDescription: "{cm:Shortcuts}"; Flags: unchecked

[Run]
Filename: "{app}\ImeLayoutRouter.exe"; Parameters: "--first-run"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
      'ImeLayoutRouter', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\ImeLayoutRouter.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ImeLayoutRouter');
end;
