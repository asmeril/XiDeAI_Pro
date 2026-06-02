; XiDeAI Pro Setup Script v5.2.2 (Optimized)
[Setup]
AppName=XiDeAI Pro
#define AppVersion "5.2.2"
AppVersion={#AppVersion}
DefaultDirName={autopf}\XiDeAI Pro
DefaultGroupName=XiDeAI Pro
UninstallDisplayIcon={app}\XiDeAI_Pro.exe
Compression=lzma2
SolidCompression=yes
OutputDir=Setup_Output
OutputBaseFilename=XiDeAI_Pro_v{#AppVersion}_Setup
SetupIconFile=xideai_icon.ico
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Ana uygulama ve DLL'ler (PDB ve gereksiz klasörler hariç)
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb, __pycache__, screenshots, cs, de, es, fr, it, ja, ko, pl, pt-BR, ru, zh-Hans, zh-Hant, createdump.exe"

[Icons]
Name: "{group}\XiDeAI Pro"; Filename: "{app}\XiDeAI_Pro.exe"
Name: "{autodesktop}\XiDeAI Pro"; Filename: "{app}\XiDeAI_Pro.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\XiDeAI_Pro.exe"; Description: "{cm:LaunchProgram,XiDeAI Pro}"; Flags: nowait postinstall skipifsilent



