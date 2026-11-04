#ifndef Edition
  #define Edition "Full"
#endif
#if Edition == "Simple"
  #define EditionName "V1"
  #define AppVersion "1.1.3"
  #define PeerEdition "V2"
  #define EditionId "{{CB27A979-27AB-40E2-A613-52CC5EFCB128}"
#else
  #define EditionName "V2"
  #define AppVersion "2.0.3"
  #define PeerEdition "V1"
  #define EditionId "{{62EBC270-35B4-4E79-A170-426D38FEAA8C}"
#endif

[Setup]
AppId={#EditionId}
AppName=IME Layout Router {#EditionName}
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
AppPublisher=31916
DefaultDirName={localappdata}\Programs\IME Layout Router {#EditionName}
DefaultGroupName=IME Layout Router {#EditionName}
OutputDir=output
OutputBaseFilename=ImeLayoutRouter-{#AppVersion}-Setup
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
english.NumericBrowser=Install the optional Chrome / Edge numeric field companion files
english.BrowserIntegration=Browser integration:
#endif
japanese.DesktopShortcut=デスクトップにショートカットを作成する
japanese.Shortcuts=追加のショートカット:
japanese.Launch=IME Layout Router を起動する
japanese.NumericBrowser=Chrome・Edge用の数字欄拡張のファイルを導入する（任意）
japanese.BrowserIntegration=ブラウザー連携:

[Files]
Source: "..\bin\{#Edition}\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "numeric-host.json"; DestDir: "{app}"; Tasks: numericbrowser; Flags: ignoreversion
Source: "..\browser-extension\*"; DestDir: "{app}\browser-extension"; Tasks: numericbrowser; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\IME Layout Router {#EditionName}"; Filename: "{app}\ImeLayoutRouter.exe"
Name: "{userdesktop}\IME Layout Router {#EditionName}"; Filename: "{app}\ImeLayoutRouter.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; GroupDescription: "{cm:Shortcuts}"; Flags: unchecked
Name: "numericbrowser"; Description: "{cm:NumericBrowser}"; GroupDescription: "{cm:BrowserIntegration}"; Flags: unchecked

[Run]
Filename: "{app}\ImeLayoutRouter.exe"; Parameters: "--first-run"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent

[Registry]
; Preserve an existing opt-in when replacing the prototype. Do not enable a
; missing entry, take another edition's slot, or change Windows startup approval.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ImeLayoutRouter"; ValueData: """{app}\ImeLayoutRouter.exe"""; Check: ShouldMigratePrototypeStartup
Root: HKCU; Subkey: "Software\31916\IMELayoutRouter\NumericHost"; ValueType: string; ValueName: "{#EditionName}"; ValueData: "{app}\numeric-host.json"; Tasks: numericbrowser
Root: HKCU; Subkey: "Software\Google\Chrome\NativeMessagingHosts\ch.imelayoutrouter.numeric"; ValueType: string; ValueData: "{app}\numeric-host.json"; Tasks: numericbrowser
Root: HKCU; Subkey: "Software\Microsoft\Edge\NativeMessagingHosts\ch.imelayoutrouter.numeric"; ValueType: string; ValueData: "{app}\numeric-host.json"; Tasks: numericbrowser

[Code]
function ShouldMigratePrototypeStartup: Boolean;
var
  Command: String;
begin
  Result := False;
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
    'ImeLayoutRouter', Command) then
    Result := CompareText(Command, '"' +
      ExpandConstant('{localappdata}\Programs\IME Layout Router\ImeLayoutRouter.exe') + '"') = 0;
end;

{ Both editions can host the same browser extension. Change a registration
  only if it still points to the copy being removed or disabled. }
procedure RestoreBrowserRegistration(const Key, OwnManifest, PeerManifest: String);
var
  Registered: String;
begin
  if RegQueryStringValue(HKCU, Key, '', Registered) and
     (CompareText(Registered, OwnManifest) = 0) then
    if (PeerManifest <> '') and FileExists(PeerManifest) then
      RegWriteStringValue(HKCU, Key, '', PeerManifest)
    else begin
      RegDeleteValue(HKCU, Key, '');
      RegDeleteKeyIfEmpty(HKCU, Key);
    end;
end;

procedure RemoveNumericCompanionRegistration();
var
  OwnManifest, Registered, PeerManifest: String;
begin
  OwnManifest := ExpandConstant('{app}\numeric-host.json');
  PeerManifest := '';
  RegQueryStringValue(HKCU, 'Software\31916\IMELayoutRouter\NumericHost', '{#PeerEdition}', PeerManifest);
  RestoreBrowserRegistration('Software\Google\Chrome\NativeMessagingHosts\ch.imelayoutrouter.numeric', OwnManifest, PeerManifest);
  RestoreBrowserRegistration('Software\Microsoft\Edge\NativeMessagingHosts\ch.imelayoutrouter.numeric', OwnManifest, PeerManifest);
  if RegQueryStringValue(HKCU, 'Software\31916\IMELayoutRouter\NumericHost', '{#EditionName}', Registered) and
     (CompareText(Registered, OwnManifest) = 0) then begin
    RegDeleteValue(HKCU, 'Software\31916\IMELayoutRouter\NumericHost', '{#EditionName}');
    RegDeleteKeyIfEmpty(HKCU, 'Software\31916\IMELayoutRouter\NumericHost');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and not WizardIsTaskSelected('numericbrowser') then
    RemoveNumericCompanionRegistration();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then begin
    RemoveNumericCompanionRegistration();
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
      'ImeLayoutRouter', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\ImeLayoutRouter.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ImeLayoutRouter');
  end;
end;
