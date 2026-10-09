; ShopBilling Professional Retail Suite — Inno Setup Script
; Target OS: Windows 7 SP1 (32-bit and 64-bit)
; Target Runtime: Microsoft .NET Framework 4.8, SQLite Interop, ClosedXML

#define MyAppName "ShopBilling"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ShopBilling Technologies"
#define MyAppExeName "ShopBilling.exe"

[Setup]
AppId={{D37F8E80-84E3-4E11-9A39-50E6B9E81944}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\installer_output
OutputBaseFilename=ShopBilling_Windows7_Setup_v1.0.0
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
MinVersion=6.1.7601
; 6.1.7601 corresponds strictly to Windows 7 Service Pack 1

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.2

[Files]
; Main Executable and Core Assemblies
Source: "..\src\ShopBilling.UI\bin\Release\ShopBilling.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\ShopBilling.UI\bin\Release\ShopBilling.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\ShopBilling.UI\bin\Release\ShopBilling.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\ShopBilling.UI\bin\Release\ShopBilling.Data.dll"; DestDir: "{app}"; Flags: ignoreversion

; SQLite Managed & Native Interop Libraries (Windows 7 SP1 x86 & x64)
Source: "..\src\ShopBilling.UI\bin\Release\System.Data.SQLite.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\ShopBilling.UI\bin\Release\x86\SQLite.Interop.dll"; DestDir: "{app}\x86"; Flags: ignoreversion
Source: "..\src\ShopBilling.UI\bin\Release\x64\SQLite.Interop.dll"; DestDir: "{app}\x64"; Flags: ignoreversion; Check: Is64BitInstallMode

; ClosedXML & OpenXML Spreadsheet Dependencies
Source: "..\src\ShopBilling.UI\bin\Release\ClosedXML.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\ShopBilling.UI\bin\Release\DocumentFormat.OpenXml.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\ShopBilling.UI\bin\Release\ExcelNumberFormat.dll"; DestDir: "{app}"; Flags: ignoreversion

; Prerequisite offline redistributables (Optional payload bundle)
; Source: "redist\ndp48-x86-x64-allos-enu.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Verify .NET Framework 4.8 is installed before proceeding on Windows 7 SP1
function IsDotNet48Detected(): Boolean;
var
  releaseValue: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', releaseValue) then
  begin
    // 528040 = .NET Framework 4.8 on Windows 7 SP1, Windows 8.1, or Windows 10
    if releaseValue >= 528040 then
      Result := True;
  end;
end;

function InitializeSetup(): Boolean;
begin
  if not IsDotNet48Detected() then
  begin
    MsgBox('Microsoft .NET Framework 4.8 is required to run ShopBilling.' + #13#10 +
           'Please install .NET Framework 4.8 (Offline Installer) and run this setup again.' + #13#10 +
           'Note: Windows 7 SP1 requires Windows Update KB2999226 (Universal C Runtime) and KB2533623.',
           mbCriticalError, MB_OK);
    Result := False;
  end
  else
  begin
    Result := True;
  end;
end;
