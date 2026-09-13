#ifndef MyAppVersion
  #error MyAppVersion is required
#endif
#if Scope == "user"
  #define ScopeId "1"
  #define AddinsRoot "{userappdata}\Autodesk\Revit\Addins"
#else
  #if Scope != "admin"
    #error Scope must be user or admin
  #endif
  #define ScopeId "2"
  #define AddinsRoot "{commonappdata}\Autodesk\Revit\Addins"
#endif

[Setup]
AppId={{A5FDD6B1-EC4B-4D3F-87EA-9D92652BCEA{#ScopeId}}
AppName=Revit DevLoader
AppVersion={#MyAppVersion}
VersionInfoVersion={#NumericVersion}
AppPublisher=Dinar Sharafutdinov
AppPublisherURL=https://github.com/sharafutdinovdi/revit-devloader
AppSupportURL=https://github.com/sharafutdinovdi/revit-devloader
AppUpdatesURL=https://github.com/sharafutdinovdi/revit-devloader
#if Scope == "user"
DefaultDirName={localappdata}\RevitDevLoader\installer
PrivilegesRequired=lowest
#else
DefaultDirName={commonpf}\RevitDevLoader
PrivilegesRequired=admin
#endif
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile={#StagingDir}\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=revit-devloader-{#MyAppVersion}-{#Scope}-setup
UninstallDisplayName=Revit DevLoader ({#Scope})
MinVersion=10.0
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousSetupType=no
CloseApplications=no
RestartApplications=no

[Types]
Name: "custom"; Description: "Select Revit years"; Flags: iscustom

[Files]
Source: "{#StagingDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Messages]
ConfirmUninstall=Remove %1?%n%nSettings, plugins, updates and run folders under LOCALAPPDATA\RevitDevLoader are preserved.
UninstallStatusLabel=Removing Revit DevLoader. Settings, plugins, updates and run folders are preserved.

[Code]
function RevitInstalled(Year: String): Boolean;
begin
  Result := FileExists(ExpandConstant('{commonpf64}\Autodesk\Revit ' + Year + '\Revit.exe'));
end;

function RevitRunning: Boolean;
var
  Locator, Services, Processes: Variant;
begin
  { Fail closed if Windows cannot enumerate processes. }
  Result := True;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Services := Locator.ConnectServer('', 'root\CIMV2');
    Processes := Services.ExecQuery('SELECT ProcessId FROM Win32_Process WHERE Name = ''Revit.exe''');
    Result := Processes.Count > 0;
  except
    Log('Cannot check Revit processes: ' + GetExceptionMessage);
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpReady) and RevitRunning then begin
    Log('Installation blocked: close Revit.exe before installing Revit DevLoader.');
    { Aborting before installation makes Inno Setup exit non-zero (observed: 1 in silent mode). }
    if WizardSilent then
      Abort;
    MsgBox('Close Revit before installing Revit DevLoader, then try again.', mbError, MB_OK);
    Result := False;
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := not RevitRunning;
  if not Result then begin
    Log('Uninstallation blocked: close Revit.exe before removing Revit DevLoader.');
    if not UninstallSilent then
      MsgBox('Close Revit before removing Revit DevLoader.', mbError, MB_OK);
  end;
end;

#include YearsInclude
