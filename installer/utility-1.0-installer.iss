; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "EcorRouge Archive Utility"

#define FileVerStr GetFileVersion("..\build\publish\EcorRouge.Archive.Utility.exe")
#define StripBuild(str VerStr) Copy(VerStr, 2, RPos(".", VerStr)-1)
#define MyAppVersion FileVerStr
;StripBuild(FileVerStr)
#define MyAppPublisher "EcorRouge"
#define MyAppURL "https://ecorrouge.com"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{57E1A837-21C1-483C-839F-A027E92065FC}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DisableDirPage=no
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=no
OutputBaseFilename=setup-archive-utility-{#MyAppVersion}-x86
Compression=lzma
SolidCompression=yes
PrivilegesRequired=none
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
;LicenseFile=eula.rtf

[Icons]
Name: "{group}\EcorRouge Archive Utility"; Filename: "{app}\EcorRouge.Archive.Utility.exe"; WorkingDir: "{app}";
Name: "{group}\Uninstall EcorRouge Archive Utility"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\EcorRouge.Archive.Utility.exe"; Description: "Run Archive Utility"; Flags: nowait postinstall skipifsilent

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]  
Name: "{app}"; Permissions: everyone-modify; 

[Files]
#include "utility-files.iss"

Source: "binaries\windowsdesktop-runtime-5.0.9-win-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall dontcopy
Source: "binaries\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall dontcopy

[Code]

function IsDotNetDetected(Version: string): boolean;
var
   OutputFileName: string;
   ResultCode: integer;
   SdkLines: TArrayOfString;
   i: integer;
begin
   OutputFileName := ExpandConstant('{tmp}\dotnet_core_versions.txt');
   Exec('cmd.exe', '/c dotnet --list-sdks > "' + OutputFileName + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

   Result:=False;
   LoadStringsFromFile(OutputFileName, SdkLines);
   DeleteFile(OutputFileName);
   for i := 0 to GetArrayLength(SdkLines) - 1 do
   begin
      if Pos(Version, SdkLines[i])=1 then begin
         Result:=True;
         Break;
      end;
   end;
   
   if Result=False then begin
      OutputFileName := ExpandConstant('{tmp}\dotnet_core_versions.txt');
      Exec('cmd.exe', '/c dotnet --list-runtimes > "' + OutputFileName + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

      LoadStringsFromFile(OutputFileName, SdkLines);
      DeleteFile(OutputFileName);
      for i := 0 to GetArrayLength(SdkLines) - 1 do
      begin
         if Pos('Microsoft.WindowsDesktop.App ' + Version, SdkLines[i])=1 then begin
            Result:=True;
            Break;
         end;
      end;
   end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var 
    arr :array[1..8] of string;
    ResultCode: integer;
begin
    if CurStep=ssPostInstall then begin
        WizardForm.StatusLabel.Caption := 'Installing VC 2015 runtime';
        ExtractTemporaryFile('vc_redist.x64.exe');
        Exec(ExpandConstant('{tmp}\vc_redist.x64.exe'), '/install /passive /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

        if not IsDotNetDetected('5.0') then begin
           WizardForm.StatusLabel.Caption := 'Installing .Net Core 5.0 runtime';
           ExtractTemporaryFile('windowsdesktop-runtime-5.0.9-win-x64.exe');
           Exec(ExpandConstant('{tmp}\windowsdesktop-runtime-5.0.9-win-x64.exe'), '/install /passive /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
        end;
    end;
end;
