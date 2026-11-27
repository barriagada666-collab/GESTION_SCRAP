#ifndef Variant
  #define Variant "selfcontained"
#endif

#define MyAppName "Gestión de Scrap"
#define MyAppShortName "GestionScrap"
#define MyAppVersion "4.1.0"
#define MyAppPublisher "Cambiaso Hnos. S.A.C."
#define MyAppURL "https://github.com/barriagada666-collab/GESTION_SCRAP"
#define MyAppExeName "GestionScrap.exe"

#if Variant == "framework"
  #define SourcePublishDir "..\publish\win-x64-fdd"
  #define OutputBase "GestionScrap_Setup_v" + MyAppVersion + "_x64_FrameworkDependent"
  #define VariantLabel "Requerido .NET 10 Desktop Runtime"
#else
  #define SourcePublishDir "..\publish\win-x64"
  #define OutputBase "GestionScrap_Setup_v" + MyAppVersion + "_x64_SelfContained"
  #define VariantLabel "Autocontenido (.NET 10)"
#endif

[Setup]
AppId={{7B6E3A2F-4F1D-4D3C-9E51-8A4C2B7F9E10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion} ({#VariantLabel})
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppShortName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\Instalador
OutputBaseFilename={#OutputBase}
SetupIconFile=assets\app_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} v{#MyAppVersion}
WizardStyle=modern
WizardSizePercent=100
WizardImageFile=assets\wizard_banner.bmp,assets\wizard_banner_2x.bmp
WizardSmallImageFile=assets\wizard_small.bmp,assets\wizard_small_2x.bmp
LicenseFile=..\LICENSE
InfoBeforeFile=InfoSoftware.rtf
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=yes
RestartApplications=yes
DisableWelcomePage=no
ShowLanguageDialog=no
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Instalador Corporativo de Gestión de Scrap v{#MyAppVersion} (.NET 10)
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoCopyright=Copyright (C) 2026 {#MyAppPublisher}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourcePublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app_icon.ico"; Comment: "Sistema de gestión de scrap y pesajes"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app_icon.ico"; Tasks: desktopicon; Comment: "Sistema de gestión de scrap y pesajes"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
