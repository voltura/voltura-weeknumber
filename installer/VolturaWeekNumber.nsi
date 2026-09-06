Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!ifndef VERSION
  !error "VERSION is required"
!endif
!ifndef PAYLOAD
  !error "PAYLOAD is required"
!endif
!ifndef OUTPUT
  !error "OUTPUT is required"
!endif
!ifndef VARIANT
  !define VARIANT "standard"
!endif
Name "Voltura WeekNumber"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\VolturaWeekNumber"
RequestExecutionLevel user
XPStyle on
ManifestDPIAware true
ManifestSupportedOS all
SetCompressor /SOLID lzma
VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "Voltura WeekNumber"
VIAddVersionKey /LANG=1033 "FileDescription" "Voltura WeekNumber Setup"
VIAddVersionKey /LANG=1033 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright (c) 2026 Voltura AB"
!define MUI_ICON "..\apps\windows\Assets\App.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\VolturaWeekNumber.exe"
!define MUI_FINISHPAGE_REBOOTLATER_DEFAULT
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Swedish"
!insertmacro MUI_LANGUAGE "German"
LangString Failed ${LANG_ENGLISH} "Setup could not complete. The previous installation was restored where possible. See the installation details and retry."
LangString Failed ${LANG_SWEDISH} "Installationen kunde inte slutföras. Den tidigare installationen återställdes om möjligt. Se installationsinformationen och försök igen."
LangString Failed ${LANG_GERMAN} "Setup konnte nicht abgeschlossen werden. Die vorherige Installation wurde nach Möglichkeit wiederhergestellt. Details prüfen und erneut versuchen."
LangString RuntimeFailed ${LANG_ENGLISH} "The .NET 10 Desktop Runtime could not be installed. Retry, or use the offline installer."
LangString RuntimeFailed ${LANG_SWEDISH} ".NET 10 Desktop Runtime kunde inte installeras. Försök igen eller använd offline-installationsprogrammet."
LangString RuntimeFailed ${LANG_GERMAN} ".NET 10 Desktop Runtime konnte nicht installiert werden. Erneut versuchen oder Offline-Installer verwenden."
LangString RemoveData ${LANG_ENGLISH} "Also remove your settings, logs, and downloaded updates?"
LangString RemoveData ${LANG_SWEDISH} "Vill du också ta bort inställningar, loggar och hämtade uppdateringar?"
LangString RemoveData ${LANG_GERMAN} "Auch Einstellungen, Protokolle und heruntergeladene Updates entfernen?"
Var AutoUpdate
Var PowerShell
Function .onInit
  ${IfNot} ${RunningX64}
    Abort "Windows x64 is required."
  ${EndIf}
  StrCpy $PowerShell "$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
  ; Use Windows modules even when setup inherits a PowerShell 7 environment.
  System::Call 'kernel32::SetEnvironmentVariableW(w "PSModulePath", w "$WINDIR\System32\WindowsPowerShell\v1.0\Modules") i.r0'
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/AUTOUPDATE" $1
  ${IfNot} ${Errors}
    StrCpy $AutoUpdate "1"
  ${EndIf}
  !insertmacro MUI_LANGDLL_DISPLAY
FunctionEnd
Section "Install"
  CreateDirectory "$LOCALAPPDATA\Voltura\WeekNumber"
  FileOpen $2 "$LOCALAPPDATA\Voltura\WeekNumber\setup.log" w
  FileClose $2
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File "maintain.ps1"
  File "prerequisite.ps1"
  File "prepare-payload.ps1"
!if "${VARIANT}" == "standard"
  nsExec::ExecToStack '"$PowerShell" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\prerequisite.ps1"'
  Pop $0
  Pop $1
  DetailPrint "$1"
  FileOpen $2 "$LOCALAPPDATA\Voltura\WeekNumber\setup.log" a
  FileSeek $2 0 END
  FileWrite $2 "$1$\r$\nExit: $0$\r$\n"
  FileClose $2
  ${If} $0 == 3010
    SetRebootFlag true
    SetErrorLevel 3010
  ${ElseIf} $0 != 0
    MessageBox MB_ICONSTOP "$(RuntimeFailed)"
    SetErrorLevel 1
    Abort
  ${EndIf}
!endif
  SetOutPath "$PLUGINSDIR\payload"
  File /r "${PAYLOAD}\*"
  WriteUninstaller "$PLUGINSDIR\payload\Uninstall.exe"
  ; The generated uninstaller is hashed after creation, before verified staging.
  nsExec::ExecToStack '"$PowerShell" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\prepare-payload.ps1" -Payload "$PLUGINSDIR\payload"'
  Pop $0
  Pop $1
  DetailPrint "$1"
  FileOpen $2 "$LOCALAPPDATA\Voltura\WeekNumber\setup.log" a
  FileSeek $2 0 END
  FileWrite $2 "$1$\r$\nExit: $0$\r$\n"
  FileClose $2
  ${If} $0 != 0
    SetErrorLevel 1
    Abort "$(Failed)"
  ${EndIf}
  nsExec::ExecToStack '"$PowerShell" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\maintain.ps1" -Payload "$PLUGINSDIR\payload" -Variant "${VARIANT}" -SetupPath "$EXEPATH"'
  Pop $0
  Pop $1
  DetailPrint "$1"
  FileOpen $2 "$LOCALAPPDATA\Voltura\WeekNumber\setup.log" a
  FileSeek $2 0 END
  FileWrite $2 "$1$\r$\nExit: $0$\r$\n"
  FileClose $2
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "$(Failed)"
    SetErrorLevel 1
    Abort
  ${EndIf}
SectionEnd
Function un.onInit
  StrCpy $PowerShell "$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
  System::Call 'kernel32::SetEnvironmentVariableW(w "PSModulePath", w "$WINDIR\System32\WindowsPowerShell\v1.0\Modules") i.r0'
FunctionEnd
Section "Uninstall"
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File "maintain.ps1"
  StrCpy $1 ""
  MessageBox MB_YESNO|MB_DEFBUTTON2 "$(RemoveData)" IDNO keepSettings
    StrCpy $1 "-RemoveSettings"
  keepSettings:
  nsExec::ExecToLog '"$PowerShell" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\maintain.ps1" -Mode Uninstall $1'
  Pop $0
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "$(Failed)"
    SetErrorLevel 1
    Abort
  ${EndIf}
SectionEnd
