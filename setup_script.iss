; XiDeAI Pro Setup Script v3.6.1 (Optimized)
[Setup]
AppName=XiDeAI Pro
#define AppVersion "3.6.1"
AppVersion=3.7.7
DefaultDirName={autopf}\XiDeAI Pro
DefaultGroupName=XiDeAI Pro
UninstallDisplayIcon={app}\XiDeAI_Pro.exe
Compression=lzma2
SolidCompression=yes
OutputDir=d:\Projects\XiDeAI_Pro\Setup_Output
OutputBaseFilename=XiDeAI_Pro_v3.7.7_Setup
SetupIconFile=d:\Projects\XiDeAI_Pro\xideai_icon.ico
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Ana uygulama ve DLL'ler (PDB ve gereksiz klasörler hariç)
Source: "d:\Projects\XiDeAI_Pro\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb, __pycache__, screenshots, cs, de, es, fr, it, ja, ko, pl, pt-BR, ru, zh-Hans, zh-Hant, createdump.exe"

[Icons]
Name: "{group}\XiDeAI Pro"; Filename: "{app}\XiDeAI_Pro.exe"
Name: "{autodesktop}\XiDeAI Pro"; Filename: "{app}\XiDeAI_Pro.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\XiDeAI_Pro.exe"; Description: "{cm:LaunchProgram,XiDeAI Pro}"; Flags: nowait postinstall skipifsilent




