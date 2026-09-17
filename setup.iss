; Inno Setup Script for Image Editor
; Compatible with Inno Setup 6.x

#define MyAppName "Image Editor"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "Eszuri"
#define MyAppURL "https://github.com/Eszuri/image-editor"
#define MyAppExeName "ImageEditor.exe"
#define MyContextMenuText "Edit image with Image Editor"
#define MyContextMenuVerb "ImageEditor"

[Setup]
; AppId uniquely identifies this application. Updates will safely replace previous installations.
AppId={{6E4A86C1-28B0-4A3C-9102-1D5B89F70D22}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Allow user to choose install for current user or all users (administrator)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline

OutputDir=Output
OutputBaseFilename=ImageEditorSetup
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; High-ratio LZMA2 compression
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; 64-bit application settings
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

; Clean upgrade and explorer shell integration handling
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "contextmenu"; Description: "Add ""{#MyContextMenuText}"" to Windows Explorer context menu"; GroupDescription: "Explorer integration:"

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Context menu for all perceived images (PNG, JPG, JPEG, BMP, WEBP, GIF, TIFF, ICO, etc.)
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\image\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: ""; ValueData: "{#MyContextMenuText}"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\image\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\image\shell\{#MyContextMenuVerb}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu; Flags: uninsdeletekey

; Explicit context menu entries for common image extensions
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.png\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: ""; ValueData: "{#MyContextMenuText}"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.png\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.png\shell\{#MyContextMenuVerb}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu; Flags: uninsdeletekey

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.jpg\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: ""; ValueData: "{#MyContextMenuText}"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.jpg\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.jpg\shell\{#MyContextMenuVerb}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu; Flags: uninsdeletekey

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.jpeg\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: ""; ValueData: "{#MyContextMenuText}"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.jpeg\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.jpeg\shell\{#MyContextMenuVerb}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu; Flags: uninsdeletekey

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.bmp\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: ""; ValueData: "{#MyContextMenuText}"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.bmp\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.bmp\shell\{#MyContextMenuVerb}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu; Flags: uninsdeletekey

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webp\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: ""; ValueData: "{#MyContextMenuText}"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webp\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webp\shell\{#MyContextMenuVerb}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu; Flags: uninsdeletekey

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.gif\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: ""; ValueData: "{#MyContextMenuText}"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.gif\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.gif\shell\{#MyContextMenuVerb}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu; Flags: uninsdeletekey

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.tiff\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: ""; ValueData: "{#MyContextMenuText}"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.tiff\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.tiff\shell\{#MyContextMenuVerb}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu; Flags: uninsdeletekey

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.tif\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: ""; ValueData: "{#MyContextMenuText}"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.tif\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.tif\shell\{#MyContextMenuVerb}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu; Flags: uninsdeletekey

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ico\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: ""; ValueData: "{#MyContextMenuText}"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ico\shell\{#MyContextMenuVerb}"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ico\shell\{#MyContextMenuVerb}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu; Flags: uninsdeletekey

; Ensure .webp has PerceivedType=image in case it is not registered
Root: HKA; Subkey: "Software\Classes\.webp"; ValueType: string; ValueName: "PerceivedType"; ValueData: "image"; Tasks: contextmenu; Flags: createvalueifdoesntexist

[UninstallDelete]
Type: files; Name: "{app}\crash.log"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

