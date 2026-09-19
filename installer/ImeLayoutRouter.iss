#ifndef Edition
  #define Edition "Full"
#endif
#if Edition == "Simple"
  #define EditionName "V1"
  #define AppVersion "1.1.0-rc.1"
  #define EditionId "{{CB27A979-27AB-40E2-A613-52CC5EFCB128}"
#else
  #define EditionName "V2"
  #define AppVersion "2.0.0-rc.1"
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

[Files]
Source: "..\bin\{#Edition}\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\IME Layout Router {#EditionName}"; Filename: "{app}\ImeLayoutRouter.exe"
Name: "{userdesktop}\IME Layout Router {#EditionName}"; Filename: "{app}\ImeLayoutRouter.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\ImeLayoutRouter.exe"; Parameters: "--first-run"; Description: "Launch IME Layout Router"; Flags: nowait postinstall skipifsilent
