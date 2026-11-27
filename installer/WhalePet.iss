; WhalePet Windows installer.
; Build with Inno Setup 6.3 or newer:
;   ISCC.exe /DAppVersion=1.1.0 installer\WhalePet.iss
; Publish output must already exist at dist\installer\publish.

#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif

#define MyAppName "鲸鱼娘"
#define MyAppPublisher "QiMingcc123"
#define MyAppURL "https://github.com/QiMingcc123/WhalePet"
#define MyAppExeName "WhalePet.exe"
#define MyAppDataDir "DSHWhalePet"
#define WebView2ClientGuid "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"

[Setup]
AppId={{A9F516B3-A121-439F-B907-017D173A09FB}
AppName={#MyAppName}
AppVerName={#MyAppName} {#AppVersion}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} 安装程序
DefaultDirName={autopf}\WhalePet
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist\installer
OutputBaseFilename=WhalePet-Setup-{#AppVersion}-x64
SetupIconFile=..\desktop\WhalePet\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
SetupLogging=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "Languages\ChineseSimplified.isl"

[Messages]
; Required custom message for Windows 10 1809 / Windows 11.
WinVersionTooLowError=本软件需要 Windows 10 版本 1809 或更高版本

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: checkedonce

[Files]
; Only the self-contained publish directory is packaged. Never scan all of bin.
Source: "..\dist\installer\publish\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载{#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "立即启动{#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  WebView2DownloadUrl = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703';

function NonZeroVersion(const Value: String): Boolean;
var
  I: Integer;
  Text: String;
begin
  Result := False;
  Text := Trim(Value);
  if Text = '' then Exit;
  // "0.0.0.0" is explicitly treated as not installed. Any non-zero numeric
  // component means a real WebView2 Runtime version.
  for I := 1 to Length(Text) do
  begin
    if (Text[I] >= '1') and (Text[I] <= '9') then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function ReadWebView2Version(const RootKey: Integer; const SubKey: String;
  out Value: String): Boolean;
begin
  Value := '';
  Result := RegQueryStringValue(RootKey, SubKey, 'pv', Value) and
    NonZeroVersion(Value);
end;

function WebView2Installed: Boolean;
var
  Value: String;
begin
  Result := False;
  if ReadWebView2Version(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\' +
    '{#WebView2ClientGuid}', Value) then
  begin
    Result := True;
    Exit;
  end;
  if ReadWebView2Version(HKLM64, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\' +
    '{#WebView2ClientGuid}', Value) then
  begin
    Result := True;
    Exit;
  end;
  if ReadWebView2Version(HKLM32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' +
    '{#WebView2ClientGuid}', Value) then
  begin
    Result := True;
    Exit;
  end;
  if ReadWebView2Version(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\' +
    '{#WebView2ClientGuid}', Value) then
  begin
    Result := True;
  end;
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not WebView2Installed then
  begin
    if MsgBox('未检测到 Microsoft Edge WebView2 Runtime。' + #13#10 +
      'Live2D 形象需要 WebView2；未安装时软件仍可使用静态图/GIF，' +
      'Live2D 会自动回退默认形象。' + #13#10#13#10 +
      '是否现在打开 WebView2 Runtime 下载页面？',
      mbConfirmation, MB_YESNO) = IDYES then
    begin
      if not ShellExec('open', WebView2DownloadUrl, '', '', SW_SHOWNORMAL,
        ewNoWait, ErrorCode) then
        MsgBox('无法打开下载页面，请手动访问：' + #13#10 + WebView2DownloadUrl,
          mbInformation, MB_OK);
    end;
  end;
end;

procedure DeleteUserData;
begin
  // Local data: settings, JSON stores, SQLite, WebView2 cache, logs and the
  // migrated novel/image files all live under this directory.
  DelTree(ExpandConstant('{localappdata}\{#MyAppDataDir}'), True, True, True);
  // Roaming and home mirrors created by PersistentStore.
  DelTree(ExpandConstant('{userappdata}\{#MyAppDataDir}'), True, True, True);
  DelTree(ExpandConstant('{%USERPROFILE}\.whalepet'), True, True, True);
  // Legacy folders from earlier releases.
  DelTree(ExpandConstant('{localappdata}\WhalePet'), True, True, True);
  DelTree(ExpandConstant('{userappdata}\WhalePet'), True, True, True);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('是否删除用户数据（设置、存档、缓存等）？',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DeleteUserData;
  end;
end;
