Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "FileFunc.nsh"
!define VERSION "2.0.18"
!define GUID "56727db4-1476-542d-9454-eb82ffd4dc27"
!define INSTALL_KEY "Software\${GUID}"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${GUID}"
Name "Pão & Caixa"
OutFile "release/PaoCaixa-Instalador-${VERSION}-x86.exe"
InstallDir "$LOCALAPPDATA\Programs\Pao e Caixa"
RequestExecutionLevel user
SetCompressor /SOLID lzma
VIProductVersion "2.0.18.0"
VIAddVersionKey /LANG=1046 "ProductName" "Pão & Caixa"
VIAddVersionKey /LANG=1046 "FileDescription" "Instalador do PDV Pão & Caixa"
VIAddVersionKey /LANG=1046 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1046 "LegalCopyright" "Pão & Caixa"
!define MUI_ICON "resources/icon.ico"
!define MUI_UNICON "resources/icon.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\PaoCaixa.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Abrir Pão & Caixa"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "PortugueseBR"
Var Attempts
Function .onInit
 SetShellVarContext current
 ${IfNot} ${AtLeastWin7}
  MessageBox MB_ICONSTOP "Esta versão precisa de Windows 7 SP1 ou mais recente."
  Abort
 ${EndIf}
 SetRegView 32
 ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
 ${If} $0 < 378389
  SetRegView 64
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
 ${EndIf}
 ${If} $0 < 378389
  MessageBox MB_ICONSTOP "Instale o .NET Framework antes de continuar.$\r$\nWindows 7 SP1 e 8.1: .NET Framework 4.8.$\r$\nWindows 8: use o .NET que vem com o Windows ou o 4.6.1.$\r$\nO guia contém os links oficiais. Nenhum banco de dados foi alterado."
  Abort
 ${EndIf}
 ${If} ${RunningX64}
  SetRegView 64
 ${Else}
  SetRegView 32
 ${EndIf}
 ReadRegStr $0 HKCU "${INSTALL_KEY}" "InstallLocation"
 ${If} $0 == ""
  ReadRegStr $0 HKCU "${UNINSTALL_KEY}" "InstallLocation"
 ${EndIf}
 ${If} $0 != ""
  StrCpy $INSTDIR $0
 ${Else}
  SetRegView 32
  ReadRegStr $0 HKCU "${INSTALL_KEY}" "InstallLocation"
  ${If} $0 != ""
   StrCpy $INSTDIR $0
  ${EndIf}
 ${EndIf}
 ${If} ${RunningX64}
  SetRegView 64
 ${EndIf}
FunctionEnd
Section "Instalar"
 StrCpy $Attempts 0
 wait_close:
 System::Call 'kernel32::OpenMutexW(i 0x100000,i 0,w "Local\PaoCaixaNative") p.r0'
 ${If} $0 != 0
  System::Call 'kernel32::CloseHandle(p r0)'
  IntOp $Attempts $Attempts + 1
  ${If} $Attempts < 30
   Sleep 1000
   Goto wait_close
  ${EndIf}
  MessageBox MB_ICONSTOP "Feche Pão & Caixa antes de instalar. Os dados foram preservados."
  Abort
 ${EndIf}
 SetOutPath "$INSTDIR"
 File "build/PaoCaixa.exe"
 File "resources/PaoCaixa.exe.config"
 File "resources/remote-control.json"
 File "vendor/sqlite/sqlite3.dll"
 File "GUIA-PaoCaixa-Windows.md"
 File /oname=icon.ico "resources/icon.ico"
 ; Only known Electron runtime files; never touch %APPDATA%\PaoCaixa.
 IfFileExists "$INSTDIR\resources\app.asar" 0 native_files
  Delete "$INSTDIR\resources\app.asar"
  Delete "$INSTDIR\resources\update-channel.json"
  Delete "$INSTDIR\resources\app-update.yml"
  Delete "$INSTDIR\resources\icon.png"
  RMDir /r "$INSTDIR\resources\app.asar.unpacked"
  RMDir "$INSTDIR\resources"
  RMDir /r "$INSTDIR\locales"
  Delete "$INSTDIR\resources.pak"
  Delete "$INSTDIR\chrome_100_percent.pak"
  Delete "$INSTDIR\chrome_200_percent.pak"
  Delete "$INSTDIR\icudtl.dat"
  Delete "$INSTDIR\snapshot_blob.bin"
  Delete "$INSTDIR\v8_context_snapshot.bin"
  Delete "$INSTDIR\chrome_crashpad_handler.exe"
  Delete "$INSTDIR\crashpad_handler.exe"
  Delete "$INSTDIR\dxcompiler.dll"
  Delete "$INSTDIR\dxil.dll"
  Delete "$INSTDIR\libEGL.dll"
  Delete "$INSTDIR\libGLESv2.dll"
  Delete "$INSTDIR\ffmpeg.dll"
  Delete "$INSTDIR\d3dcompiler_47.dll"
  Delete "$INSTDIR\vk_swiftshader.dll"
  Delete "$INSTDIR\vk_swiftshader_icd.json"
  Delete "$INSTDIR\vulkan-1.dll"
  Delete "$INSTDIR\LICENSE.electron.txt"
  Delete "$INSTDIR\LICENSES.chromium.html"
  Delete "$INSTDIR\Uninstall Pao e Caixa.exe"
 native_files:
 WriteUninstaller "$INSTDIR\Uninstall PaoCaixa.exe"
 CreateShortcut "$DESKTOP\Pão & Caixa.lnk" "$INSTDIR\PaoCaixa.exe" "" "$INSTDIR\icon.ico"
 CreateShortcut "$SMPROGRAMS\Pão & Caixa.lnk" "$INSTDIR\PaoCaixa.exe" "" "$INSTDIR\icon.ico"
 WriteRegStr HKCU "${INSTALL_KEY}" "InstallLocation" "$INSTDIR"
 WriteRegStr HKCU "${INSTALL_KEY}" "ShortcutName" "Pão & Caixa"
 WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "Pão & Caixa"
 WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
 WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\PaoCaixa.exe,0"
 WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
 WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall PaoCaixa.exe"'
 WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
 WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
 IfSilent 0 section_end
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/NOLAUNCH" $1
  IfErrors +2 0
   Goto section_end
  Exec '"$INSTDIR\PaoCaixa.exe"'
 section_end:
SectionEnd
Section "Uninstall"
 SetShellVarContext current
 ${If} ${RunningX64}
  SetRegView 64
 ${EndIf}
 System::Call 'kernel32::OpenMutexW(i 0x100000,i 0,w "Local\PaoCaixaNative") p.r0'
 ${If} $0 != 0
  System::Call 'kernel32::CloseHandle(p r0)'
  MessageBox MB_ICONSTOP "Feche Pão & Caixa antes de desinstalar."
  Abort
 ${EndIf}
 Delete "$INSTDIR\PaoCaixa.exe"
 Delete "$INSTDIR\PaoCaixa.exe.config"
 Delete "$INSTDIR\remote-control.json"
 Delete "$INSTDIR\sqlite3.dll"
 Delete "$INSTDIR\icon.ico"
 Delete "$INSTDIR\GUIA-PaoCaixa-Windows.md"
 Delete "$INSTDIR\Uninstall PaoCaixa.exe"
 Delete "$DESKTOP\Pão & Caixa.lnk"
 Delete "$SMPROGRAMS\Pão & Caixa.lnk"
 RMDir "$INSTDIR"
 DeleteRegKey HKCU "${INSTALL_KEY}"
 DeleteRegKey HKCU "${UNINSTALL_KEY}"
 ; Database, account credentials, drafts and backups intentionally remain.
SectionEnd
