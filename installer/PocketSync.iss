; PocketSync — script Inno Setup
; Compilare: ISCC.exe PocketSync.iss (din folderul installer\)

#define AppName "PocketSync"
#define AppVersion "1.1.1"
#define AppExe "PocketSync.TrayApp.exe"
#define AppPort "5000"

[Setup]
AppId={{7E1C3A52-9B84-4E4B-A1D2-POCKETSYNC10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=papura-octavian
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=output
OutputBaseFilename=PocketSync-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; admin: necesar pentru Program Files si regula de firewall
PrivilegesRequired=admin
WizardStyle=modern
SetupIconFile=..\Photos\Icon.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Creează shortcut pe Desktop"; GroupDescription: "Shortcut-uri:"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Dezinstalează {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; regula de firewall: aplicatia ruleaza fara admin, installerul (cu admin) o pune o singura data
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""{#AppName}"" dir=in action=allow protocol=TCP localport={#AppPort} profile=private"; Flags: runhidden
Filename: "{app}\{#AppExe}"; Description: "Pornește {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""{#AppName}"""; Flags: runhidden; RunOnceId: "DelFirewallRule"
