
#define MyAppName "Veltrix"
#define MyAppVersion "1.0.0"
#define MyAppExeName "Veltrix.exe"
#define MyAppPublisher "Veltrix"

[Setup]
AppId=Veltrix.NetworkMonitor
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppVerName=Veltrix v{#MyAppVersion}

DefaultDirName={autopf}\Veltrix
DefaultGroupName=Veltrix

DisableProgramGroupPage=yes
AllowNoIcons=yes

OutputDir=Installer
OutputBaseFilename=Veltrix_Setup_v1.0.0

SetupIconFile=Assets\Veltrix.ico
UninstallDisplayIcon={app}\Veltrix.exe
UninstallDisplayName=Veltrix Network Monitor

Compression=lzma2
SolidCompression=yes
WizardStyle=modern

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

MinVersion=10.0.17763
PrivilegesRequired=admin

CloseApplications=yes
RestartApplications=no
Uninstallable=yes

VersionInfoVersion=1.0.0.0
VersionInfoDescription=Veltrix Network Monitor Installer
VersionInfoProductName=Veltrix
VersionInfoCompany=Veltrix
VersionInfoProductVersion=1.0.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Veltrix"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall Veltrix"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Veltrix"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Veltrix"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN VeltrixStartup /F"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveVeltrixStartup"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Veltrix"; Flags: deletevalue uninsdeletevalue