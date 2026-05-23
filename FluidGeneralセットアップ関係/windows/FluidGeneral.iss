[Setup]
AppName=Fluid General
AppVersion=1.1.0
DefaultDirName={autopf}\FluidGeneral
DefaultGroupName=Fluid General
UninstallDisplayIcon={app}\fluid-general.Avalonia.exe
Compression=lzma2
SolidCompression=yes
OutputDir=userdocs:Inno Setup Builds
OutputBaseFilename=FluidGeneralSetup
SetupIconFile=C:\Project\fluid-general\fluid-general.Avalonia\Assets\large_ico.ico

[Files]
; dotnet publishで出力された単一exeを指定
Source: "C:\Project\fluid-general\fluid-general.Avalonia\bin\Release\net10.0\win-x64\publish\fluid-general.Avalonia.exe"; DestDir: "{app}"; Flags: ignoreversion
; 依存するアセット（Soundファイル等）やサブフォルダがある場合、ここでコピー
Source: "C:\Project\fluid-general\fluid-general.Avalonia\bin\Release\net10.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Fluid General"; Filename: "{app}\fluid-general.Avalonia.exe"
Name: "{autodesktop}\Fluid General"; Filename: "{app}\fluid-general.Avalonia.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
Filename: "{app}\fluid-general.Avalonia.exe"; Description: "{cm:LaunchProgram,Fluid General}"; Flags: nowait postinstall skipifsilent
