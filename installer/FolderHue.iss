; FolderHue installer.
;
; Per-user install, no elevation: everything lands under %LOCALAPPDATA% and every registry key
; goes to HKEY_CURRENT_USER. A shell extension does not need administrator rights, and asking for
; them would be one more reason for people not to install it.
;
; Two things here are not decoration:
;
;   * Explorer keeps FolderHue.Shell.dll loaded once it has shown the menu. Files cannot be
;     replaced or deleted while it does. On an upgrade it therefore has to be stopped BEFORE
;     the files are copied, which is what PrepareToInstall does - measured: with the stop
;     scheduled after the copy, as a [Run] entry, the upgrade aborts with exit code 5.
;
;     A second stop follows the install, so that Explorer picks up the freshly written
;     verb. A first install only needs that second one, hence the test on the existing DLL.
;
;     Only taskkill runs: Windows restarts the shell by itself (AutoRestartShell). Wrapping it
;     as "cmd /c taskkill & start explorer.exe" looks equivalent and is not - the hidden cmd
;     never returns, and a silent install hangs there forever.
;
;   * Uninstalling never touches the user's folders unless they ask. The "reset" task is
;     unchecked by default, and only ever removes what FolderHue itself wrote.

#define AppName "FolderHue"
#define AppVersion "1.0.0"
#define AppPublisher "FolderHue"
#define AppUrl "https://github.com/0xpvcd/FolderHue"
#define AppExe "FolderHue.App.exe"

[Setup]
; Never reuse this GUID for another product: it is what identifies an upgrade.
AppId={{7F3C1E52-9B2A-4D57-8E14-2A6D0C93B5F1}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}

DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Windows 10 1809 is the floor stated in the README; IExplorerCommand verbs long predate it.
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

OutputDir=..\artifacts
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
SetupIconFile=FolderHue.ico
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[CustomMessages]
en.ResetFoldersTask=Also restore the original icon of every coloured folder
en.LaunchApp=Open FolderHue settings
fr.ResetFoldersTask=Rendre aussi son icone d'origine a chaque dossier colorise
fr.LaunchApp=Ouvrir les reglages de FolderHue

[Files]
; Les .pdb et les .xml de documentation ne servent a rien a l'execution : le seul PDB du shell
; pese 16 Mo, et un PDB embarque les chemins absolus de la machine de compilation.
Source: "..\artifacts\app\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml"; \
    Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Run]
; Order matters: the palette has to exist before the menu can show a swatch for it, and
; --register generates whatever is missing before writing the keys.
Filename: "{app}\{#AppExe}"; Parameters: "--register"; StatusMsg: "Registering the context menu..."; Flags: runhidden waituntilterminated
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im explorer.exe"; StatusMsg: "Restarting File Explorer..."; Flags: runhidden waituntilterminated skipifdoesntexist
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Runs before the files are removed, which is the only moment the executable still exists.
Filename: "{app}\{#AppExe}"; Parameters: "--unregister"; RunOnceId: "UnregisterShell"; Flags: runhidden waituntilterminated
; Explorer holds FolderHue.Shell.dll open; without this the DLL survives the uninstall.
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im explorer.exe"; RunOnceId: "RestartExplorer"; Flags: runhidden waituntilterminated skipifdoesntexist

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
var
  ResetFoldersCheckBox: TNewCheckBox;

{ Frees FolderHue.Shell.dll before the copy starts. Explorer only loads it once a context
  menu has been opened, so a shell that has just restarted does not hold it. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\FolderHue.Shell.dll')) then
  begin
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im explorer.exe', '',
         SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(2500);
  end;
end;

{ The uninstaller offers, but never assumes: the colours are the user's data, and removing the
  program is not a request to undo them. The box starts unchecked on purpose. }
procedure InitializeUninstallProgressForm();
begin
  ResetFoldersCheckBox := TNewCheckBox.Create(UninstallProgressForm);
  ResetFoldersCheckBox.Parent := UninstallProgressForm.InnerPage;
  ResetFoldersCheckBox.Left := UninstallProgressForm.StatusLabel.Left;
  ResetFoldersCheckBox.Top := UninstallProgressForm.StatusLabel.Top + ScaleY(48);
  ResetFoldersCheckBox.Width := UninstallProgressForm.InnerPage.ClientWidth - ScaleX(32);
  ResetFoldersCheckBox.Height := ScaleY(17);
  ResetFoldersCheckBox.Caption := ExpandConstant('{cm:ResetFoldersTask}');
  ResetFoldersCheckBox.Checked := False;
end;

function ShouldResetFolders(): Boolean;
begin
  Result := (ResetFoldersCheckBox <> nil) and ResetFoldersCheckBox.Checked;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if (CurUninstallStep = usUninstall) and ShouldResetFolders() then
  begin
    Exec(ExpandConstant('{app}\{#AppExe}'), '--reset-all', '',
         SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
