; X'iDeAI Installer Script

#define MyAppName "XiDeAI Pro"
#define MyAppVersion "5.1.0"
#define MyAppPublisher "iDeAI Labs"
#define MyAppExeName "XiDeAI_Pro.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-1234-567890ABCDEF}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=XiDeAI_v{#MyAppVersion}_Setup
SetupIconFile=xideai_icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
; Uninstaller settings - ICO dosyas?n? kullan (EXE yerine daha g?venilir)
UninstallDisplayIcon={app}\xideai_icon.ico
UninstallDisplayName={#MyAppName}

[Code]
function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{A1B2C3D4-E5F6-7890-1234-567890ABCDEF}_is1';
  sUnInstallString := '';
  if not RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;

function InitializeSetup(): Boolean;
var
  V: Integer;
  iResultCode: Integer;
  sUnInstallString: String;
begin
  Result := True;
  
  sUnInstallString := GetUninstallString();
  if sUnInstallString <> '' then
  begin
    V := MsgBox('?nceki s?r?m tespit edildi. Temiz kurulum i?in kald?r?ls?n m??' + #13#10 + '(Ayarlar?n?z korunacakt?r)', mbInformation, MB_YESNO);
    if V = IDYES then
    begin
      sUnInstallString := RemoveQuotes(sUnInstallString);
      if Exec(sUnInstallString, '/SILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, iResultCode) then
        Result := True
      else
        MsgBox('Kald?rma ba?ar?s?z oldu. Manuel kald?rman?z gerekebilir.', mbError, MB_OK);
    end;
  end;
end;

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; .NET projesi publish edildi?inde olu?an dosyalar? al?r
Source: "Dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "Scripts\screenshots\*,Logs\*,*.log"
; Icon dosyas? (Windows Uygulamalar listesinde g?r?nmesi i?in)
Source: "xideai_icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\xideai_icon.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\xideai_icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
















































































































































































