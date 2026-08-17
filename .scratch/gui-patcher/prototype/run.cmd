@echo off
REM PROTOTYPE runner. Compiles the throwaway WinForms shell and launches it.
REM Usage:  run.cmd                 (starts on C, the chosen variant)
REM         run.cmd A               (starts on A or B for comparison)
REM         run.cmd C --diesel      (h.sie's Diesel Damage flag simulated ON)
REM         run.cmd C --unsupported (starts on the allowlist refusal)
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set OUT=%~dp0ApePatcherPrototype.exe
set ART=%~dp0..\..\..\data\art\blinkingape.png
"%CSC%" /nologo /target:winexe /out:"%OUT%" ^
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  /resource:"%ART%",ape.png ^
  "%~dp0ApePatcherPrototype.cs"
if errorlevel 1 exit /b 1
start "" "%OUT%" %*
