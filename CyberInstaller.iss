; Cyber Installer Script

[Setup]
AppName=Cyber
AppVersion=1.0
DefaultDirName={commonpf}\Cyber
DefaultGroupName=Cyber
OutputDir=output
OutputBaseFilename=CyberInstaller
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin

[Files]
; Service binary (publish output from ThinkerAgent)
Source: "backend\src\ThinkerAgent\bin\Release\net10.0\win-x64\publish\*"; \
    DestDir: "{app}\service"; \
    Flags: ignoreversion recursesubdirs
; Deploy a default appsettings.json into ProgramData\Cyber
Source: "backend\src\ThinkerAgent\bin\Release\net10.0\win-x64\publish\appsettings.json"; \
    DestDir: "{commonappdata}\Cyber"; \
    Flags: ignoreversion
; Control app binary (publish output from YourCyber - cross-platform Avalonia app)
Source: "backend\src\YourCyber\bin\Release\net10.0\win-x64\publish\*"; \
    DestDir: "{app}\control"; \
    Flags: ignoreversion recursesubdirs

[Dirs]
Name: "{app}\service"
Name: "{app}\control"

[Icons]
Name: "{group}\YourCyber"; Filename: "{app}\control\YourCyber.exe"
Name: "{group}\Uninstall Cyber"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\control\YourCyber.exe"; Description: "Launch YourCyber"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "YourCyber"; \
    ValueData: """{app}\control\YourCyber.exe"""; Flags: uninsdeletevalue

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop ThinkerAgent"; StatusMsg: "Stopping Windows Service..."; RunOnceId: "StopService"
Filename: "sc.exe"; Parameters: "delete ThinkerAgent"; StatusMsg: "Removing Windows Service..."; RunOnceId: "DeleteService"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    { Copy ProgramData appsettings.json over the service copy so both stay in sync }
    FileCopy(
      ExpandConstant('{commonappdata}\Cyber\appsettings.json'),
      ExpandConstant('{app}\service\appsettings.json'),
      False);

    Exec(ExpandConstant('sc.exe'),
           'create ThinkerAgent binPath= "' + ExpandConstant('{app}\service\ThinkerAgent.exe') + '" start= auto',
           '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('sc.exe'), 'start ThinkerAgent', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
