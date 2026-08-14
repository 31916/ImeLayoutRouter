[Setup]
AppId={{A3D58B85-8A19-4A94-8F29-0C3D1F8F1F21}
AppName=IME Layout Router
AppVersion=1.0.0
AppPublisher=31916
DefaultDirName={localappdata}\Programs\IME Layout Router
DefaultGroupName=IME Layout Router
OutputDir=output
OutputBaseFilename=ImeLayoutRouter-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\ImeLayoutRouter.exe

[Files]
Source: "..\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\IME Layout Router"; Filename: "{app}\ImeLayoutRouter.exe"
Name: "{userdesktop}\IME Layout Router"; Filename: "{app}\ImeLayoutRouter.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\ImeLayoutRouter.exe"; Parameters: "--first-run"; Description: "Launch IME Layout Router"; Flags: nowait postinstall skipifsilent