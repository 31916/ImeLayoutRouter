#define AppVersion "3.0.0-preview.1"

[Setup]
AppId={{FE9A7A80-FB65-4212-8E5E-A6EA250336B8}
AppName=IME Layout Router V3 Preview
AppVersion={#AppVersion}
VersionInfoVersion=3.0.0
AppPublisher=31916
DefaultDirName={localappdata}\Programs\IME Layout Router V3
DefaultGroupName=IME Layout Router V3
OutputDir=output-v3
OutputBaseFilename=ImeLayoutRouter-{#AppVersion}-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\ImeLayoutRouter.exe
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[CustomMessages]
english.DesktopShortcut=Create a desktop shortcut
english.Shortcuts=Additional shortcuts:
english.Launch=Launch IME Layout Router V3 Preview
japanese.DesktopShortcut=デスクトップにショートカットを作成する
japanese.Shortcuts=追加のショートカット:
japanese.Launch=IME Layout Router V3 試験版を起動する

[Files]
Source: "..\bin\Guard\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\IME Layout Router V3"; Filename: "{app}\ImeLayoutRouter.exe"
Name: "{userdesktop}\IME Layout Router V3"; Filename: "{app}\ImeLayoutRouter.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; GroupDescription: "{cm:Shortcuts}"; Flags: unchecked

[Run]
Filename: "{app}\ImeLayoutRouter.exe"; Parameters: "--first-run"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeUninstall(): Boolean;
var
  Code: Integer;
  Executable: String;
begin
  Result := True;
  Executable := ExpandConstant('{app}\ImeLayoutRouter.exe');
  if FileExists(Executable) then
  begin
    Result := Exec(Executable, '--shutdown', '', SW_HIDE, ewWaitUntilTerminated, Code);
    if Result then Result := Code = 0;
    if not Result then
      SuppressibleMsgBox('V3 could not restore the composition windows. Close the affected input application and retry uninstalling.', mbError, MB_OK, IDOK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
      'ImeLayoutRouterV3', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\ImeLayoutRouter.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ImeLayoutRouterV3');
end;
